import CoreImage
import Foundation

/// Writes an `.fcpxml` project (plus colour-balanced stills when enabled) for Final Cut Pro.
public struct FinalCutProExporter: Sendable {
    public let processor: ImageProcessor

    public init(processor: ImageProcessor = ImageProcessor()) {
        self.processor = processor
    }

    /// - Returns: The URL of the written `.fcpxml` file.
    @discardableResult
    public func export(
        images: [URL],
        settings: ExportSettings,
        projectName: String,
        to outputFolder: URL,
        progress: @escaping @Sendable (Double) -> Void = { _ in }
    ) async throws -> URL {
        guard !images.isEmpty else { throw StopMotionError.noImages }

        // Colour-balanced copies live next to the project; otherwise the originals are used.
        let mediaFolder = outputFolder.appendingPathComponent("\(projectName) Media", isDirectory: true)
        if settings.colorBalance {
            try FileManager.default.createDirectory(at: mediaFolder, withIntermediateDirectories: true)
        }

        var stills: [FCPXMLStill] = []
        stills.reserveCapacity(images.count)
        for (index, url) in images.enumerated() {
            try Task.checkCancellation()
            var image = try processor.load(url)
            var stillURL = url
            if settings.colorBalance {
                image = processor.grayWorldBalanced(image)
                let name = String(format: "%04d_%@.jpg", index + 1, url.deletingPathExtension().lastPathComponent)
                stillURL = mediaFolder.appendingPathComponent(name)
                try processor.writeJPEG(image, to: stillURL)
            }
            stills.append(FCPXMLStill(url: stillURL, width: Int(image.extent.width), height: Int(image.extent.height)))
            progress(Double(index + 1) / Double(images.count))
            await Task.yield()
        }

        let builder = FCPXMLBuilder(
            projectName: projectName,
            width: settings.resolution.width,
            height: settings.resolution.height,
            timing: settings.timing
        )
        let projectURL = outputFolder.appendingPathComponent("\(projectName).fcpxml")
        try builder.build(stills: stills).write(to: projectURL, atomically: true, encoding: .utf8)
        return projectURL
    }
}

/// The files an export produced.
public struct ExportResult: Equatable, Sendable {
    public var videoURL: URL?
    public var projectURL: URL?

    public init(videoURL: URL? = nil, projectURL: URL? = nil) {
        self.videoURL = videoURL
        self.projectURL = projectURL
    }
}

/// Runs the video and/or Final Cut Pro exports for a set of images.
public struct StopMotionExporter: Sendable {
    public let processor: ImageProcessor

    public init(processor: ImageProcessor = ImageProcessor()) {
        self.processor = processor
    }

    public func export(
        images: [URL],
        settings: ExportSettings,
        name: String,
        to outputFolder: URL,
        progress: @escaping @Sendable (Double) -> Void = { _ in }
    ) async throws -> ExportResult {
        guard !images.isEmpty else { throw StopMotionError.noImages }

        let videoShare = settings.outputKind == .both ? 0.85 : 1.0
        var result = ExportResult()

        if settings.outputKind.includesVideo {
            let url = outputFolder.appendingPathComponent(name).appendingPathExtension(settings.codec.fileExtension)
            try await VideoRenderer(processor: processor).render(images: images, settings: settings, to: url) { value in
                progress(value * videoShare)
            }
            result.videoURL = url
        }

        if settings.outputKind.includesFinalCutPro {
            let start = settings.outputKind.includesVideo ? videoShare : 0
            result.projectURL = try await FinalCutProExporter(processor: processor).export(
                images: images,
                settings: settings,
                projectName: name,
                to: outputFolder
            ) { value in
                progress(start + value * (1 - start))
            }
        }

        progress(1)
        return result
    }

    /// Default base name, e.g. `new_slide_show-2026-09-25`, matching the command-line tool.
    public static func defaultName(date: Date = Date()) -> String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "yyyy-MM-dd"
        return "new_slide_show-\(formatter.string(from: date))"
    }
}
