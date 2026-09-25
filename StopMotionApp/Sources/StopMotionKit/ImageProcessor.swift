import CoreGraphics
import CoreImage
import CoreImage.CIFilterBuiltins
import CoreVideo
import Foundation
import ImageIO

public enum StopMotionError: LocalizedError, Equatable {
    case noImages
    case unreadableImage(URL)
    case writerFailed(String)

    public var errorDescription: String? {
        switch self {
        case .noImages:
            return "There are no supported images to export."
        case .unreadableImage(let url):
            return "Couldn't read \(url.lastPathComponent)."
        case .writerFailed(let message):
            return "Video export failed: \(message)"
        }
    }
}

/// Core Image pipeline shared by the video and Final Cut Pro exports.
public final class ImageProcessor: @unchecked Sendable {
    public let context: CIContext
    public let workingColorSpace = CGColorSpace(name: CGColorSpace.sRGB)!

    public init(context: CIContext = CIContext(options: [.cacheIntermediates: false])) {
        self.context = context
    }

    /// Loads an image with its EXIF orientation applied.
    public func load(_ url: URL) throws -> CIImage {
        guard let image = CIImage(contentsOf: url, options: [.applyOrientationProperty: true]) else {
            throw StopMotionError.unreadableImage(url)
        }
        // Move the origin to (0, 0) so later transforms are simple.
        return image.transformed(by: CGAffineTransform(translationX: -image.extent.minX, y: -image.extent.minY))
    }

    /// Gray-world white balance: scales each channel so the image's average colour becomes neutral.
    public func grayWorldBalanced(_ image: CIImage) -> CIImage {
        let extent = image.extent
        let average = CIFilter.areaAverage()
        average.inputImage = image
        average.extent = extent
        guard let averageImage = average.outputImage else { return image }

        var pixel = [Float](repeating: 0, count: 4)
        context.render(
            averageImage,
            toBitmap: &pixel,
            rowBytes: MemoryLayout<Float>.size * 4,
            bounds: CGRect(x: 0, y: 0, width: 1, height: 1),
            format: .RGBAf,
            colorSpace: nil
        )

        let red = max(Double(pixel[0]), 1.0 / 255.0)
        let green = max(Double(pixel[1]), 1.0 / 255.0)
        let blue = max(Double(pixel[2]), 1.0 / 255.0)
        let gray = (red + green + blue) / 3.0

        let matrix = CIFilter.colorMatrix()
        matrix.inputImage = image
        matrix.rVector = CIVector(x: CGFloat(gray / red), y: 0, z: 0, w: 0)
        matrix.gVector = CIVector(x: 0, y: CGFloat(gray / green), z: 0, w: 0)
        matrix.bVector = CIVector(x: 0, y: 0, z: CGFloat(gray / blue), w: 0)
        matrix.aVector = CIVector(x: 0, y: 0, z: 0, w: 1)
        matrix.biasVector = CIVector(x: 0, y: 0, z: 0, w: 0)
        return (matrix.outputImage ?? image).cropped(to: extent)
    }

    /// Scales `image` to fit `size` keeping its aspect ratio, centred on black (letterbox/pillarbox).
    public func fit(_ image: CIImage, into size: CGSize) -> CIImage {
        let canvas = CGRect(origin: .zero, size: size)
        let extent = image.extent
        guard extent.width > 0, extent.height > 0 else {
            return CIImage(color: .black).cropped(to: canvas)
        }

        let scale = min(size.width / extent.width, size.height / extent.height)
        let lanczos = CIFilter.lanczosScaleTransform()
        lanczos.inputImage = image
        lanczos.scale = Float(scale)
        lanczos.aspectRatio = 1
        let scaled = lanczos.outputImage ?? image.transformed(by: CGAffineTransform(scaleX: scale, y: scale))

        let scaledExtent = scaled.extent
        let x = ((size.width - scaledExtent.width) / 2).rounded() - scaledExtent.minX
        let y = ((size.height - scaledExtent.height) / 2).rounded() - scaledExtent.minY
        let centred = scaled.transformed(by: CGAffineTransform(translationX: x, y: y))

        return centred
            .composited(over: CIImage(color: .black).cropped(to: canvas))
            .cropped(to: canvas)
    }

    /// Load → optional colour balance → fit to the output canvas, rendered once so it can be
    /// reused for every frame it appears in.
    public func preparedFrame(for url: URL, size: CGSize, colorBalance: Bool) throws -> CIImage {
        var image = try load(url)
        if colorBalance {
            image = grayWorldBalanced(image)
        }
        let fitted = fit(image, into: size)
        guard let cgImage = context.createCGImage(fitted, from: CGRect(origin: .zero, size: size), format: .RGBA8, colorSpace: workingColorSpace) else {
            throw StopMotionError.unreadableImage(url)
        }
        return CIImage(cgImage: cgImage)
    }

    /// A linear dissolve between two prepared frames.
    public func dissolve(from outgoing: CIImage, to incoming: CIImage, amount: Double) -> CIImage {
        let transition = CIFilter.dissolveTransition()
        transition.inputImage = outgoing
        transition.targetImage = incoming
        transition.time = Float(amount)
        return (transition.outputImage ?? incoming).cropped(to: outgoing.extent)
    }

    public func render(_ image: CIImage, to pixelBuffer: CVPixelBuffer) {
        let bounds = CGRect(x: 0, y: 0, width: CVPixelBufferGetWidth(pixelBuffer), height: CVPixelBufferGetHeight(pixelBuffer))
        context.render(image, to: pixelBuffer, bounds: bounds, colorSpace: workingColorSpace)
    }

    /// Writes a JPEG (quality 0.95) of `image` for Final Cut Pro to import.
    public func writeJPEG(_ image: CIImage, to url: URL) throws {
        let quality = CIImageRepresentationOption(rawValue: kCGImageDestinationLossyCompressionQuality as String)
        let options: [CIImageRepresentationOption: Any] = [quality: 0.95]
        try context.writeJPEGRepresentation(of: image, to: url, colorSpace: workingColorSpace, options: options)
    }
}
