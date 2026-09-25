import Foundation

/// A still image placed on the Final Cut Pro timeline.
public struct FCPXMLStill: Equatable, Sendable {
    public var url: URL
    public var width: Int
    public var height: Int

    public init(url: URL, width: Int, height: Int) {
        self.url = url
        self.width = width
        self.height = height
    }
}

/// Builds an FCPXML 1.10 document (Final Cut Pro 10.6 and later).
///
/// Each still sits on the primary storyline for `timing.framesPerImage` frames and, when
/// `timing.crossfadeFrames` is non-zero, a Cross Dissolve is centred on every cut. The output
/// matches `ImgConcat`'s `FcpxmlBuilder`, so both tools produce the same timeline.
public struct FCPXMLBuilder: Sendable {
    public static let version = "1.10"
    /// Final Cut Pro's built-in Cross Dissolve transition.
    public static let crossDissolveUID = "FxPlug:4731E73A-8DAC-4113-9A30-AE85B1761265"

    public var projectName: String
    public var eventName: String
    public var width: Int
    public var height: Int
    public var timing: FrameTiming

    public init(projectName: String, eventName: String = "StopMotion", width: Int, height: Int, timing: FrameTiming) {
        self.projectName = projectName
        self.eventName = eventName
        self.width = width
        self.height = height
        self.timing = timing
    }

    public enum BuildError: Error, Equatable {
        case noStills
    }

    public func build(stills: [FCPXMLStill]) throws -> String {
        guard !stills.isEmpty else { throw BuildError.noStills }

        let fps = timing.frameRate
        let perImage = timing.framesPerImage
        let crossfade = timing.crossfadeFrames

        var resources: [String] = []
        let sequenceFormatID = "r1"
        resources.append(
            #"<format id="\#(sequenceFormatID)" name="\#(Self.escape(Self.sequenceFormatName(width: width, height: height, fps: fps)))" frameDuration="\#(Self.time(frames: 1, fps: fps))" width="\#(width)" height="\#(height)" colorSpace="1-1-1 (Rec. 709)"/>"#
        )

        var nextID = 2
        var transitionEffectID: String?
        if crossfade > 0 && stills.count > 1 {
            let id = "r\(nextID)"
            nextID += 1
            transitionEffectID = id
            resources.append(#"<effect id="\#(id)" name="Cross Dissolve" uid="\#(Self.crossDissolveUID)"/>"#)
        }

        // Stills share a rate-undefined format per distinct size.
        var stillFormats: [String: String] = [:]
        var assetIDs: [String] = []
        for still in stills {
            let sizeKey = "\(still.width)x\(still.height)"
            let formatID: String
            if let existing = stillFormats[sizeKey] {
                formatID = existing
            } else {
                formatID = "r\(nextID)"
                nextID += 1
                stillFormats[sizeKey] = formatID
                resources.append(
                    #"<format id="\#(formatID)" name="FFVideoFormatRateUndefined" width="\#(still.width)" height="\#(still.height)" colorSpace="1-13-1"/>"#
                )
            }

            let assetID = "r\(nextID)"
            nextID += 1
            assetIDs.append(assetID)
            let name = Self.escape(still.url.deletingPathExtension().lastPathComponent)
            let src = Self.escape(still.url.standardizedFileURL.absoluteString)
            resources.append(
                """
                <asset id="\(assetID)" name="\(name)" start="0s" duration="0s" hasVideo="1" format="\(formatID)" videoSources="1">
                    <media-rep kind="original-media" src="\(src)"/>
                </asset>
                """
            )
        }

        var spine: [String] = []
        for (index, still) in stills.enumerated() {
            let clipStart = index * perImage
            if index > 0, let effectID = transitionEffectID {
                // Centre the dissolve over the cut; stills have unlimited handles.
                spine.append(
                    """
                    <transition name="Cross Dissolve" offset="\(Self.time(frames: clipStart - crossfade / 2, fps: fps))" duration="\(Self.time(frames: crossfade, fps: fps))">
                        <filter-video ref="\(effectID)" name="Cross Dissolve"/>
                    </transition>
                    """
                )
            }
            let name = Self.escape(still.url.deletingPathExtension().lastPathComponent)
            spine.append(
                #"<video ref="\#(assetIDs[index])" offset="\#(Self.time(frames: clipStart, fps: fps))" name="\#(name)" start="0s" duration="\#(Self.time(frames: perImage, fps: fps))"/>"#
            )
        }

        let totalFrames = timing.totalFrames(imageCount: stills.count)
        return """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE fcpxml>
        <fcpxml version="\(Self.version)">
            <resources>
        \(Self.indent(resources, level: 2))
            </resources>
            <library>
                <event name="\(Self.escape(eventName))">
                    <project name="\(Self.escape(projectName))">
                        <sequence format="\(sequenceFormatID)" duration="\(Self.time(frames: totalFrames, fps: fps))" tcStart="0s" tcFormat="NDF">
                            <spine>
        \(Self.indent(spine, level: 7))
                            </spine>
                        </sequence>
                    </project>
                </event>
            </library>
        </fcpxml>

        """
    }

    /// Formats a frame count as an FCPXML rational time, e.g. 60 frames at 30fps → "60/30s".
    public static func time(frames: Int, fps: Int) -> String {
        frames == 0 ? "0s" : "\(frames)/\(fps)s"
    }

    static func sequenceFormatName(width: Int, height: Int, fps: Int) -> String {
        switch (width, height) {
        case (1920, 1080): return "FFVideoFormat1080p\(fps)"
        case (1280, 720): return "FFVideoFormat720p\(fps)"
        default: return "FFVideoFormat\(width)x\(height)p\(fps)"
        }
    }

    static func escape(_ value: String) -> String {
        var result = ""
        result.reserveCapacity(value.count)
        for character in value {
            switch character {
            case "&": result += "&amp;"
            case "<": result += "&lt;"
            case ">": result += "&gt;"
            case "\"": result += "&quot;"
            case "'": result += "&apos;"
            default: result.append(character)
            }
        }
        return result
    }

    private static func indent(_ blocks: [String], level: Int) -> String {
        let pad = String(repeating: "    ", count: level)
        return blocks
            .flatMap { $0.split(separator: "\n", omittingEmptySubsequences: false) }
            .map { pad + $0 }
            .joined(separator: "\n")
    }
}
