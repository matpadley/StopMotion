import Foundation

/// Converts second-based settings into whole frames, mirroring the command-line tool:
/// every image occupies `framesPerImage` frames and the last `crossfadeFrames` of each
/// slot dissolve into the next image.
public struct FrameTiming: Equatable, Sendable {
    public let frameRate: Int
    public let framesPerImage: Int
    public let crossfadeFrames: Int

    public init(frameRate: Int, secondsPerImage: Double, crossfadeSeconds: Double) {
        let rate = max(1, frameRate)
        let perImage = max(1, Int((secondsPerImage * Double(rate)).rounded()))
        let crossfade = max(0, Int((crossfadeSeconds * Double(rate)).rounded()))
        self.frameRate = rate
        self.framesPerImage = perImage
        // A dissolve can't be longer than the image it leaves.
        self.crossfadeFrames = min(crossfade, perImage - 1)
    }

    public init(frameRate: Int, framesPerImage: Int, crossfadeFrames: Int) {
        self.frameRate = max(1, frameRate)
        self.framesPerImage = max(1, framesPerImage)
        self.crossfadeFrames = min(max(0, crossfadeFrames), self.framesPerImage - 1)
    }

    public func totalFrames(imageCount: Int) -> Int {
        max(0, imageCount) * framesPerImage
    }

    public func duration(imageCount: Int) -> TimeInterval {
        Double(totalFrames(imageCount: imageCount)) / Double(frameRate)
    }

    /// Frames an image is shown on its own before it dissolves into the next one.
    public func holdFrames(isLast: Bool) -> Int {
        isLast ? framesPerImage : framesPerImage - crossfadeFrames
    }

    /// Blend amount (0 = outgoing image, 1 = incoming image) for a frame of the dissolve.
    public func blendRatio(crossfadeFrame frame: Int) -> Double {
        guard crossfadeFrames > 1 else { return 1 }
        return Double(frame) / Double(crossfadeFrames - 1)
    }
}
