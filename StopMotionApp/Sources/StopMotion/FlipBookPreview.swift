import StopMotionKit
import SwiftUI

/// Plays the frames at the chosen timing without rendering a movie, so timing can be tuned
/// before exporting.
struct FlipBookPreview: View {
    let images: [URL]
    let timing: FrameTiming

    @State private var isPlaying = true
    @State private var startDate = Date()
    @State private var pausedFrame = 0

    var body: some View {
        VStack(spacing: 0) {
            TimelineView(.animation(minimumInterval: 1.0 / Double(timing.frameRate), paused: !isPlaying)) { context in
                let frame = isPlaying ? currentFrame(at: context.date) : pausedFrame
                let current = slot(forFrame: frame)
                ZStack {
                    Color.black
                    ThumbnailImage(url: images[current.index], maxPixelSize: 1280)
                    if let next = current.nextIndex {
                        ThumbnailImage(url: images[next], maxPixelSize: 1280)
                            .opacity(current.blend)
                    }
                }
                .overlay(alignment: .bottomTrailing) {
                    Text("\(current.index + 1) / \(images.count)")
                        .font(.caption.monospacedDigit())
                        .padding(6)
                        .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 6))
                        .padding(8)
                }
            }

            HStack {
                Button {
                    togglePlayback()
                } label: {
                    Image(systemName: isPlaying ? "pause.fill" : "play.fill")
                        .frame(width: 20)
                }
                Button {
                    pausedFrame = 0
                    startDate = Date()
                } label: {
                    Image(systemName: "backward.end.fill")
                }
                Spacer()
                Text("Preview · \(timing.frameRate) fps")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            .buttonStyle(.borderless)
            .padding(8)
            .background(.bar)
        }
        .onChange(of: images) { _, _ in restart() }
        .onChange(of: timing) { _, _ in restart() }
    }

    private var totalFrames: Int { max(1, timing.totalFrames(imageCount: images.count)) }

    private func currentFrame(at date: Date) -> Int {
        let elapsed = max(0, date.timeIntervalSince(startDate))
        return (pausedFrame + Int(elapsed * Double(timing.frameRate))) % totalFrames
    }

    private func slot(forFrame frame: Int) -> (index: Int, nextIndex: Int?, blend: Double) {
        let index = min(images.count - 1, frame / timing.framesPerImage)
        let frameInImage = frame % timing.framesPerImage
        let hold = timing.holdFrames(isLast: index == images.count - 1)
        guard frameInImage >= hold else { return (index, nil, 0) }
        return (index, index + 1, timing.blendRatio(crossfadeFrame: frameInImage - hold))
    }

    private func togglePlayback() {
        if isPlaying {
            pausedFrame = currentFrame(at: Date())
        } else {
            startDate = Date()
        }
        isPlaying.toggle()
    }

    private func restart() {
        pausedFrame = 0
        startDate = Date()
    }
}
