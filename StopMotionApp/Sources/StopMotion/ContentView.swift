import AVKit
import StopMotionKit
import SwiftUI

struct ContentView: View {
    @EnvironmentObject private var model: ExportModel
    @State private var isDropTargeted = false

    var body: some View {
        NavigationSplitView {
            SettingsSidebar()
                .navigationSplitViewColumnWidth(min: 300, ideal: 320, max: 400)
        } detail: {
            detail
                .dropDestination(for: URL.self) { urls, _ in
                    model.handleDrop(urls)
                } isTargeted: { isDropTargeted = $0 }
                .overlay {
                    if isDropTargeted {
                        RoundedRectangle(cornerRadius: 12)
                            .strokeBorder(Color.accentColor, style: StrokeStyle(lineWidth: 3, dash: [8]))
                            .padding(8)
                    }
                }
        }
        .toolbar {
            ToolbarItemGroup(placement: .primaryAction) {
                Button {
                    model.chooseSourceFolder()
                } label: {
                    Label("Open Folder", systemImage: "folder")
                }
                if model.isExporting {
                    Button(role: .cancel) {
                        model.cancelExport()
                    } label: {
                        Label("Cancel", systemImage: "xmark.circle")
                    }
                } else {
                    Button {
                        model.startExport()
                    } label: {
                        Label("Export", systemImage: "square.and.arrow.up")
                    }
                    .disabled(!model.canExport)
                }
            }
        }
        .navigationSubtitle(model.images.isEmpty ? "" : model.durationDescription)
    }

    @ViewBuilder
    private var detail: some View {
        if model.images.isEmpty {
            ContentUnavailableView {
                Label("No Frames", systemImage: "film.stack")
            } description: {
                Text("Open a folder of photos, or drop a folder or images here. Frames are ordered by file name.")
            } actions: {
                Button("Open Folder…") { model.chooseSourceFolder() }
                    .buttonStyle(.borderedProminent)
            }
        } else {
            VStack(spacing: 0) {
                StatusBanner()
                HSplitView {
                    PreviewPane()
                        .frame(minWidth: 320, idealWidth: 520)
                    FrameGrid()
                        .frame(minWidth: 260)
                }
            }
        }
    }
}

/// Export progress / result / error shown above the preview.
private struct StatusBanner: View {
    @EnvironmentObject private var model: ExportModel

    var body: some View {
        switch model.phase {
        case .idle:
            EmptyView()
        case .exporting(let progress):
            HStack {
                ProgressView(value: progress) {
                    Text("Exporting… \(Int(progress * 100))%")
                }
                Button("Cancel") { model.cancelExport() }
            }
            .padding()
            .background(.bar)
        case .finished(let result):
            HStack(spacing: 12) {
                Image(systemName: "checkmark.circle.fill").foregroundStyle(.green)
                Text("Export complete")
                Spacer()
                if let video = result.videoURL {
                    Button("Show Video") { model.revealInFinder(video) }
                }
                if let project = result.projectURL {
                    Button("Show Project") { model.revealInFinder(project) }
                    if model.isFinalCutProInstalled {
                        Button("Open in Final Cut Pro") { model.openInFinalCutPro(project) }
                            .buttonStyle(.borderedProminent)
                    }
                }
                Button {
                    model.dismissResult()
                } label: {
                    Image(systemName: "xmark")
                }
                .buttonStyle(.borderless)
            }
            .padding()
            .background(.bar)
        case .failed(let message):
            HStack {
                Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.yellow)
                Text(message)
                Spacer()
                Button {
                    model.dismissResult()
                } label: {
                    Image(systemName: "xmark")
                }
                .buttonStyle(.borderless)
            }
            .padding()
            .background(.bar)
        }
    }
}

/// Plays the finished video, or a live flip-book of the frames before exporting.
private struct PreviewPane: View {
    @EnvironmentObject private var model: ExportModel

    var body: some View {
        Group {
            if case .finished(let result) = model.phase, let video = result.videoURL {
                MoviePreview(url: video)
            } else {
                FlipBookPreview(images: model.images, timing: model.settings.timing)
            }
        }
        .background(Color.black)
    }
}

private struct MoviePreview: View {
    let url: URL
    @State private var player: AVPlayer? = nil

    var body: some View {
        VideoPlayer(player: player)
            .task(id: url) {
                let player = AVPlayer(url: url)
                self.player = player
                player.play()
            }
    }
}
