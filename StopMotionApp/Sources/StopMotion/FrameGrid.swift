import SwiftUI

/// All frames in export order. Right-click a frame to leave it out of the export.
struct FrameGrid: View {
    @EnvironmentObject private var model: ExportModel

    private let columns = [GridItem(.adaptive(minimum: 120, maximum: 180), spacing: 8)]

    var body: some View {
        ScrollView {
            LazyVGrid(columns: columns, spacing: 8) {
                ForEach(Array(model.images.enumerated()), id: \.element) { index, url in
                    VStack(spacing: 4) {
                        ThumbnailImage(url: url, maxPixelSize: 320, contentMode: .fill)
                            .frame(height: 90)
                            .frame(maxWidth: .infinity)
                            .clipped()
                            .clipShape(RoundedRectangle(cornerRadius: 6))
                        Text("\(index + 1). \(url.lastPathComponent)")
                            .font(.caption2)
                            .lineLimit(1)
                            .truncationMode(.middle)
                            .foregroundStyle(.secondary)
                    }
                    .contextMenu {
                        Button("Remove from Export") { model.remove(url) }
                        Button("Show in Finder") { model.revealInFinder(url) }
                    }
                    .help(url.lastPathComponent)
                }
            }
            .padding(12)
        }
        .overlay(alignment: .bottom) {
            if let folder = model.sourceFolder {
                HStack {
                    Image(systemName: "folder")
                    Text(folder.lastPathComponent).lineLimit(1)
                    Spacer()
                    Button("Reload") { model.reloadFolder() }
                        .buttonStyle(.borderless)
                        .disabled(model.isExporting)
                }
                .font(.caption)
                .padding(8)
                .background(.bar)
            }
        }
    }
}
