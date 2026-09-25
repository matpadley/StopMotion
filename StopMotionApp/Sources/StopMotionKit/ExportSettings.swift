import Foundation

/// What an export produces.
public enum OutputKind: String, CaseIterable, Identifiable, Codable, Sendable {
    case video
    case finalCutPro
    case both

    public var id: String { rawValue }

    public var displayName: String {
        switch self {
        case .video: return "Video"
        case .finalCutPro: return "Final Cut Pro Project"
        case .both: return "Video + Final Cut Pro"
        }
    }

    public var includesVideo: Bool { self != .finalCutPro }
    public var includesFinalCutPro: Bool { self != .video }
}

/// Output canvas size.
public enum Resolution: String, CaseIterable, Identifiable, Codable, Sendable {
    case hd720
    case hd1080
    case uhd4K

    public var id: String { rawValue }

    public var width: Int {
        switch self {
        case .hd720: return 1280
        case .hd1080: return 1920
        case .uhd4K: return 3840
        }
    }

    public var height: Int {
        switch self {
        case .hd720: return 720
        case .hd1080: return 1080
        case .uhd4K: return 2160
        }
    }

    public var displayName: String {
        switch self {
        case .hd720: return "720p (1280×720)"
        case .hd1080: return "1080p (1920×1080)"
        case .uhd4K: return "4K (3840×2160)"
        }
    }
}

/// Video codec for the rendered movie.
public enum VideoCodec: String, CaseIterable, Identifiable, Codable, Sendable {
    case h264
    case hevc
    case proRes422

    public var id: String { rawValue }

    public var displayName: String {
        switch self {
        case .h264: return "H.264"
        case .hevc: return "HEVC (H.265)"
        case .proRes422: return "Apple ProRes 422"
        }
    }

    /// ProRes only goes in a QuickTime container.
    public var fileExtension: String { self == .proRes422 ? "mov" : "mp4" }
}

/// All user-tunable options for an export.
public struct ExportSettings: Codable, Equatable, Sendable {
    /// Frame rates offered for rendered video.
    public static let videoFrameRates = [12, 15, 24, 25, 30, 50, 60]
    /// Integer frame rates Final Cut Pro accepts for a project timeline.
    public static let finalCutProFrameRates = [24, 25, 30, 50, 60]

    /// Frame rates valid for the selected output.
    public static func frameRates(for kind: OutputKind) -> [Int] {
        kind.includesFinalCutPro ? finalCutProFrameRates : videoFrameRates
    }

    public var outputKind: OutputKind = .video
    public var resolution: Resolution = .hd1080
    public var frameRate: Int = 30
    /// How long each image is on screen, in seconds (includes the crossfade into the next image).
    public var secondsPerImage: Double = 2.0
    /// Length of the crossfade between images, in seconds. Zero gives hard cuts.
    public var crossfadeSeconds: Double = 0.5
    /// Apply gray-world white balance to every image, like the command-line tool.
    public var colorBalance: Bool = true
    public var codec: VideoCodec = .h264

    public init() {}

    public var timing: FrameTiming {
        FrameTiming(frameRate: frameRate, secondsPerImage: secondsPerImage, crossfadeSeconds: crossfadeSeconds)
    }
}
