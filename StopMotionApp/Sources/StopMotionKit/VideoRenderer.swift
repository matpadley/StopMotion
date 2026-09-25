import AVFoundation
import CoreImage
import CoreVideo
import Foundation

/// Renders a folder of stills to a movie with AVAssetWriter.
public struct VideoRenderer: Sendable {
    public let processor: ImageProcessor

    public init(processor: ImageProcessor = ImageProcessor()) {
        self.processor = processor
    }

    /// Writes the movie to `outputURL`, replacing any existing file.
    /// `progress` is called with values from 0 to 1. Honours task cancellation.
    public func render(
        images: [URL],
        settings: ExportSettings,
        to outputURL: URL,
        progress: @escaping @Sendable (Double) -> Void = { _ in }
    ) async throws {
        guard !images.isEmpty else { throw StopMotionError.noImages }

        let timing = settings.timing
        let width = settings.resolution.width
        let height = settings.resolution.height
        let size = CGSize(width: width, height: height)
        let totalFrames = timing.totalFrames(imageCount: images.count)

        try? FileManager.default.removeItem(at: outputURL)
        let fileType: AVFileType = settings.codec == .proRes422 ? .mov : .mp4
        let writer = try AVAssetWriter(outputURL: outputURL, fileType: fileType)

        let input = AVAssetWriterInput(mediaType: .video, outputSettings: Self.outputSettings(for: settings))
        input.expectsMediaDataInRealTime = false
        let adaptor = AVAssetWriterInputPixelBufferAdaptor(
            assetWriterInput: input,
            sourcePixelBufferAttributes: [
                kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA,
                kCVPixelBufferWidthKey as String: width,
                kCVPixelBufferHeightKey as String: height,
                kCVPixelBufferIOSurfacePropertiesKey as String: [String: Any]()
            ]
        )
        guard writer.canAdd(input) else {
            throw StopMotionError.writerFailed("The writer can't accept \(settings.codec.displayName) video.")
        }
        writer.add(input)

        guard writer.startWriting() else {
            throw StopMotionError.writerFailed(writer.error?.localizedDescription ?? "Couldn't start writing.")
        }
        writer.startSession(atSourceTime: .zero)

        var frameIndex = 0
        func append(_ image: CIImage, count: Int) async throws {
            guard count > 0 else { return }
            let buffer = try makePixelBuffer(adaptor: adaptor, width: width, height: height)
            processor.render(image, to: buffer)
            // A held image is rendered once and appended for each frame it's on screen.
            for _ in 0..<count {
                try Task.checkCancellation()
                while !input.isReadyForMoreMediaData {
                    if writer.status == .failed {
                        throw StopMotionError.writerFailed(writer.error?.localizedDescription ?? "Unknown error.")
                    }
                    try await Task.sleep(nanoseconds: 2_000_000)
                }
                let time = CMTime(value: CMTimeValue(frameIndex), timescale: CMTimeScale(timing.frameRate))
                guard adaptor.append(buffer, withPresentationTime: time) else {
                    throw StopMotionError.writerFailed(writer.error?.localizedDescription ?? "Couldn't append frame \(frameIndex).")
                }
                frameIndex += 1
                progress(Double(frameIndex) / Double(totalFrames))
            }
        }

        do {
            var current = try processor.preparedFrame(for: images[0], size: size, colorBalance: settings.colorBalance)
            for index in images.indices {
                try Task.checkCancellation()
                let isLast = index == images.count - 1
                try await append(current, count: timing.holdFrames(isLast: isLast))
                guard !isLast else { break }

                let next = try processor.preparedFrame(for: images[index + 1], size: size, colorBalance: settings.colorBalance)
                for frame in 0..<timing.crossfadeFrames {
                    let blended = processor.dissolve(from: current, to: next, amount: timing.blendRatio(crossfadeFrame: frame))
                    try await append(blended, count: 1)
                }
                current = next
            }

            input.markAsFinished()
            // Give the last frame its full duration.
            writer.endSession(atSourceTime: CMTime(value: CMTimeValue(frameIndex), timescale: CMTimeScale(timing.frameRate)))
            await withCheckedContinuation { (continuation: CheckedContinuation<Void, Never>) in
                writer.finishWriting { continuation.resume() }
            }
            if writer.status != .completed {
                throw StopMotionError.writerFailed(writer.error?.localizedDescription ?? "Couldn't finish the movie.")
            }
        } catch {
            if writer.status == .writing {
                writer.cancelWriting()
            }
            try? FileManager.default.removeItem(at: outputURL)
            throw error
        }
    }

    private func makePixelBuffer(adaptor: AVAssetWriterInputPixelBufferAdaptor, width: Int, height: Int) throws -> CVPixelBuffer {
        var buffer: CVPixelBuffer?
        if let pool = adaptor.pixelBufferPool {
            CVPixelBufferPoolCreatePixelBuffer(nil, pool, &buffer)
        } else {
            let attributes: [String: Any] = [kCVPixelBufferIOSurfacePropertiesKey as String: [String: Any]()]
            CVPixelBufferCreate(nil, width, height, kCVPixelFormatType_32BGRA, attributes as CFDictionary, &buffer)
        }
        guard let buffer else {
            throw StopMotionError.writerFailed("Couldn't allocate a frame buffer.")
        }
        return buffer
    }

    static func outputSettings(for settings: ExportSettings) -> [String: Any] {
        var output: [String: Any] = [
            AVVideoWidthKey: settings.resolution.width,
            AVVideoHeightKey: settings.resolution.height,
            AVVideoColorPropertiesKey: [
                AVVideoColorPrimariesKey: AVVideoColorPrimaries_ITU_R_709_2,
                AVVideoTransferFunctionKey: AVVideoTransferFunction_ITU_R_709_2,
                AVVideoYCbCrMatrixKey: AVVideoYCbCrMatrix_ITU_R_709_2
            ]
        ]
        switch settings.codec {
        case .h264:
            output[AVVideoCodecKey] = AVVideoCodecType.h264
            output[AVVideoCompressionPropertiesKey] = [
                AVVideoProfileLevelKey: AVVideoProfileLevelH264HighAutoLevel,
                AVVideoAverageBitRateKey: bitRate(for: settings)
            ]
        case .hevc:
            output[AVVideoCodecKey] = AVVideoCodecType.hevc
            output[AVVideoCompressionPropertiesKey] = [
                AVVideoAverageBitRateKey: bitRate(for: settings) * 2 / 3
            ]
        case .proRes422:
            output[AVVideoCodecKey] = AVVideoCodecType.proRes422
        }
        return output
    }

    /// Roughly 0.2 bits per pixel per frame – generous for mostly-still footage.
    private static func bitRate(for settings: ExportSettings) -> Int {
        let pixelsPerSecond = settings.resolution.width * settings.resolution.height * settings.frameRate
        return max(4_000_000, pixelsPerSecond / 5)
    }
}
