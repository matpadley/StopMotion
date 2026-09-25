import XCTest
@testable import StopMotionKit

final class FCPXMLBuilderTests: XCTestCase {
    private func stills(_ count: Int) -> [FCPXMLStill] {
        (1...count).map { FCPXMLStill(url: URL(fileURLWithPath: "/tmp/frame \($0).jpg"), width: 4000, height: 3000) }
    }

    private func document(stills: [FCPXMLStill], crossfadeFrames: Int = 15) throws -> XMLDocument {
        let builder = FCPXMLBuilder(
            projectName: "Test & Project",
            width: 1920,
            height: 1080,
            timing: FrameTiming(frameRate: 30, framesPerImage: 60, crossfadeFrames: crossfadeFrames)
        )
        return try XMLDocument(xmlString: try builder.build(stills: stills))
    }

    private func values(_ document: XMLDocument, _ xpath: String) throws -> [String] {
        try document.nodes(forXPath: xpath).compactMap(\.stringValue)
    }

    func testClipsAreBackToBack() throws {
        let doc = try document(stills: stills(3))
        XCTAssertEqual(try values(doc, "//spine/video/@offset"), ["0s", "60/30s", "120/30s"])
        XCTAssertEqual(try values(doc, "//spine/video/@duration"), ["60/30s", "60/30s", "60/30s"])
        XCTAssertEqual(try values(doc, "//sequence/@duration"), ["180/30s"])
    }

    func testCrossDissolveIsCentredOnEachCut() throws {
        let doc = try document(stills: stills(3))
        XCTAssertEqual(try values(doc, "//spine/transition/@offset"), ["53/30s", "113/30s"])
        XCTAssertEqual(try values(doc, "//spine/transition/@duration"), ["15/30s", "15/30s"])
        XCTAssertEqual(try values(doc, "//effect/@uid"), [FCPXMLBuilder.crossDissolveUID])
        let effectID = try values(doc, "//effect/@id").first
        XCTAssertEqual(Set(try values(doc, "//transition/filter-video/@ref")), [effectID!])
    }

    func testNoCrossfadeMeansHardCuts() throws {
        let doc = try document(stills: stills(3), crossfadeFrames: 0)
        XCTAssertTrue(try doc.nodes(forXPath: "//transition").isEmpty)
        XCTAssertTrue(try doc.nodes(forXPath: "//effect").isEmpty)
    }

    func testAssetsUseFileURLsAndUniqueIDs() throws {
        let doc = try document(stills: stills(2))
        let sources = try values(doc, "//asset/media-rep/@src")
        XCTAssertEqual(sources, ["file:///tmp/frame%201.jpg", "file:///tmp/frame%202.jpg"])

        let ids = try values(doc, "//@id")
        XCTAssertEqual(ids.count, Set(ids).count)
        XCTAssertEqual(try values(doc, "//spine/video/@ref"), try values(doc, "//asset/@id"))
    }

    func testFormatsAndEscaping() throws {
        let doc = try document(stills: [
            FCPXMLStill(url: URL(fileURLWithPath: "/tmp/a.jpg"), width: 4000, height: 3000),
            FCPXMLStill(url: URL(fileURLWithPath: "/tmp/b.jpg"), width: 4000, height: 3000),
            FCPXMLStill(url: URL(fileURLWithPath: "/tmp/c.jpg"), width: 3000, height: 4000)
        ])
        XCTAssertEqual(try values(doc, "/fcpxml/@version"), [FCPXMLBuilder.version])
        XCTAssertEqual(try values(doc, "//format[@id='r1']/@name"), ["FFVideoFormat1080p30"])
        XCTAssertEqual(try values(doc, "//format[@id='r1']/@frameDuration"), ["1/30s"])
        XCTAssertEqual(try doc.nodes(forXPath: "//format[@name='FFVideoFormatRateUndefined']").count, 2)
        XCTAssertEqual(try values(doc, "//project/@name"), ["Test & Project"])
    }

    func testEmptyInputThrows() {
        let builder = FCPXMLBuilder(projectName: "Empty", width: 1920, height: 1080, timing: FrameTiming(frameRate: 30, framesPerImage: 30, crossfadeFrames: 0))
        XCTAssertThrowsError(try builder.build(stills: [])) { error in
            XCTAssertEqual(error as? FCPXMLBuilder.BuildError, .noStills)
        }
    }
}
