import XCTest
@testable import StopMotionKit

final class FrameTimingTests: XCTestCase {
    func testSecondsAreConvertedToWholeFrames() {
        let timing = FrameTiming(frameRate: 30, secondsPerImage: 2, crossfadeSeconds: 0.5)
        XCTAssertEqual(timing.framesPerImage, 60)
        XCTAssertEqual(timing.crossfadeFrames, 15)
        XCTAssertEqual(timing.totalFrames(imageCount: 3), 180)
        XCTAssertEqual(timing.duration(imageCount: 3), 6, accuracy: 0.0001)
    }

    func testStopMotionOnTwos() {
        let timing = FrameTiming(frameRate: 24, secondsPerImage: 2.0 / 24.0, crossfadeSeconds: 0)
        XCTAssertEqual(timing.framesPerImage, 2)
        XCTAssertEqual(timing.crossfadeFrames, 0)
    }

    func testCrossfadeIsShorterThanImage() {
        let timing = FrameTiming(frameRate: 30, secondsPerImage: 1, crossfadeSeconds: 5)
        XCTAssertEqual(timing.crossfadeFrames, 29)

        let single = FrameTiming(frameRate: 30, framesPerImage: 1, crossfadeFrames: 10)
        XCTAssertEqual(single.crossfadeFrames, 0)
    }

    func testEveryImageFillsItsSlot() {
        let timing = FrameTiming(frameRate: 30, framesPerImage: 60, crossfadeFrames: 15)
        XCTAssertEqual(timing.holdFrames(isLast: false) + timing.crossfadeFrames, timing.framesPerImage)
        XCTAssertEqual(timing.holdFrames(isLast: true), timing.framesPerImage)
    }

    func testBlendRatioRunsFromZeroToOne() {
        let timing = FrameTiming(frameRate: 30, framesPerImage: 60, crossfadeFrames: 5)
        XCTAssertEqual(timing.blendRatio(crossfadeFrame: 0), 0)
        XCTAssertEqual(timing.blendRatio(crossfadeFrame: 2), 0.5)
        XCTAssertEqual(timing.blendRatio(crossfadeFrame: 4), 1)
    }

    func testFinalCutProFrameRatesAreRestricted() {
        XCTAssertFalse(ExportSettings.frameRates(for: .finalCutPro).contains(12))
        XCTAssertFalse(ExportSettings.frameRates(for: .both).contains(15))
        XCTAssertTrue(ExportSettings.frameRates(for: .video).contains(12))
    }

    func testImagesAreSortedLikeFinder() {
        let urls = ["IMG_10.jpg", "IMG_2.jpg", "IMG_1.jpg"].map { URL(fileURLWithPath: "/tmp/\($0)") }
        XCTAssertEqual(ImageLibrary.sorted(urls).map(\.lastPathComponent), ["IMG_1.jpg", "IMG_2.jpg", "IMG_10.jpg"])
        XCTAssertTrue(ImageLibrary.isSupportedImage(URL(fileURLWithPath: "/tmp/a.HEIC")))
        XCTAssertFalse(ImageLibrary.isSupportedImage(URL(fileURLWithPath: "/tmp/a.mov")))
    }
}
