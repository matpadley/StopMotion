import AVFoundation
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers
import XCTest
@testable import StopMotionKit

final class ExportIntegrationTests: XCTestCase {
    private var folder: URL!

    override func setUpWithError() throws {
        folder = FileManager.default.temporaryDirectory.appendingPathComponent("StopMotionTests-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: folder)
    }

    /// Writes a solid-colour PNG of the given size.
    @discardableResult
    private func writeImage(named name: String, width: Int, height: Int, red: CGFloat, green: CGFloat, blue: CGFloat) throws -> URL {
        let context = try XCTUnwrap(CGContext(
            data: nil, width: width, height: height, bitsPerComponent: 8, bytesPerRow: 0,
            space: CGColorSpace(name: CGColorSpace.sRGB)!,
            bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
        ))
        context.setFillColor(red: red, green: green, blue: blue, alpha: 1)
        context.fill(CGRect(x: 0, y: 0, width: width, height: height))
        let image = try XCTUnwrap(context.makeImage())

        let url = folder.appendingPathComponent(name)
        let destination = try XCTUnwrap(CGImageDestinationCreateWithURL(url as CFURL, UTType.png.identifier as CFString, 1, nil))
        CGImageDestinationAddImage(destination, image, nil)
        XCTAssertTrue(CGImageDestinationFinalize(destination))
        return url
    }

    private func sampleImages() throws -> [URL] {
        try writeImage(named: "frame_2.png", width: 640, height: 480, red: 0.2, green: 0.8, blue: 0.2)
        try writeImage(named: "frame_10.png", width: 480, height: 640, red: 0.2, green: 0.2, blue: 0.8)
        try writeImage(named: "frame_1.png", width: 800, height: 450, red: 0.8, green: 0.2, blue: 0.2)
        return try ImageLibrary.images(in: folder)
    }

    func testImagesAreFoundInOrder() throws {
        let images = try sampleImages()
        XCTAssertEqual(images.map(\.lastPathComponent), ["frame_1.png", "frame_2.png", "frame_10.png"])
    }

    func testExportsVideoAndFinalCutProject() async throws {
        let images = try sampleImages()
        var settings = ExportSettings()
        settings.outputKind = .both
        settings.resolution = .hd720
        settings.frameRate = 24
        settings.secondsPerImage = 0.5
        settings.crossfadeSeconds = 0.25

        let outputFolder = folder.appendingPathComponent("out")
        try FileManager.default.createDirectory(at: outputFolder, withIntermediateDirectories: true)

        let result = try await StopMotionExporter().export(images: images, settings: settings, name: "Test Export", to: outputFolder)

        // Video: 3 images × 12 frames at 24fps = 1.5s at 1280×720.
        let videoURL = try XCTUnwrap(result.videoURL)
        XCTAssertEqual(videoURL.pathExtension, "mp4")
        let asset = AVURLAsset(url: videoURL)
        let duration = try await asset.load(.duration)
        XCTAssertEqual(duration.seconds, 1.5, accuracy: 0.05)
        let tracks = try await asset.loadTracks(withMediaType: .video)
        let track = try XCTUnwrap(tracks.first)
        let size = try await track.load(.naturalSize)
        XCTAssertEqual(size, CGSize(width: 1280, height: 720))

        // Project: colour-balanced stills copied next to the .fcpxml at their original size.
        let projectURL = try XCTUnwrap(result.projectURL)
        XCTAssertEqual(projectURL.lastPathComponent, "Test Export.fcpxml")
        let media = try FileManager.default.contentsOfDirectory(atPath: outputFolder.appendingPathComponent("Test Export Media").path).sorted()
        XCTAssertEqual(media, ["0001_frame_1.jpg", "0002_frame_2.jpg", "0003_frame_10.jpg"])

        let doc = try XMLDocument(contentsOf: projectURL)
        XCTAssertEqual(try doc.nodes(forXPath: "//spine/video").count, 3)
        XCTAssertEqual(try doc.nodes(forXPath: "//spine/transition").count, 2)
        let widths = try doc.nodes(forXPath: "//format[@name='FFVideoFormatRateUndefined']/@width").compactMap(\.stringValue)
        XCTAssertEqual(Set(widths), ["800", "640", "480"])
    }

    func testFinalCutProjectCanReferenceOriginals() async throws {
        let images = try sampleImages()
        var settings = ExportSettings()
        settings.outputKind = .finalCutPro
        settings.colorBalance = false

        let result = try await StopMotionExporter().export(images: images, settings: settings, name: "Originals", to: folder)

        XCTAssertNil(result.videoURL)
        let doc = try XMLDocument(contentsOf: try XCTUnwrap(result.projectURL))
        let sources = try doc.nodes(forXPath: "//media-rep/@src").compactMap(\.stringValue)
        XCTAssertEqual(sources.compactMap { URL(string: $0)?.lastPathComponent }, ["frame_1.png", "frame_2.png", "frame_10.png"])
        XCTAssertFalse(FileManager.default.fileExists(atPath: folder.appendingPathComponent("Originals Media").path))
    }

    func testGrayWorldBalanceNeutralisesColourCast() throws {
        let url = try writeImage(named: "cast.png", width: 64, height: 64, red: 0.8, green: 0.4, blue: 0.2)
        let processor = ImageProcessor()
        let balanced = processor.grayWorldBalanced(try processor.load(url))

        var pixel = [UInt8](repeating: 0, count: 4)
        processor.context.render(balanced, toBitmap: &pixel, rowBytes: 4, bounds: CGRect(x: 0, y: 0, width: 1, height: 1), format: .RGBA8, colorSpace: processor.workingColorSpace)
        XCTAssertEqual(Int(pixel[0]), Int(pixel[1]), accuracy: 3)
        XCTAssertEqual(Int(pixel[1]), Int(pixel[2]), accuracy: 3)
    }

    func testCancellationRemovesPartialVideo() async throws {
        let images = try sampleImages()
        var settings = ExportSettings()
        settings.secondsPerImage = 60 // long enough to still be running when cancelled
        let output = folder.appendingPathComponent("cancelled.mp4")

        let task = Task {
            try await VideoRenderer().render(images: images, settings: settings, to: output)
        }
        try await Task.sleep(nanoseconds: 200_000_000)
        task.cancel()
        do {
            try await task.value
            XCTFail("Expected cancellation")
        } catch is CancellationError {
            XCTAssertFalse(FileManager.default.fileExists(atPath: output.path))
        }
    }
}
