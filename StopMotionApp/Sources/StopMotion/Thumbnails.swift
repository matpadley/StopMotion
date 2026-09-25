import AppKit
import ImageIO
import SwiftUI

/// Downsampled images for the grid and flip-book, decoded off the main thread and cached.
final class ThumbnailCache: @unchecked Sendable {
    static let shared = ThumbnailCache()

    private let cache: NSCache<NSString, NSImage> = {
        let cache = NSCache<NSString, NSImage>()
        cache.totalCostLimit = 512 * 1024 * 1024
        return cache
    }()

    func cachedImage(for url: URL, maxPixelSize: Int) -> NSImage? {
        cache.object(forKey: key(url, maxPixelSize))
    }

    func image(for url: URL, maxPixelSize: Int) async -> NSImage? {
        if let cached = cachedImage(for: url, maxPixelSize: maxPixelSize) {
            return cached
        }
        let task = Task.detached(priority: .userInitiated) { () -> (NSImage, Int)? in
            let options: [CFString: Any] = [
                kCGImageSourceCreateThumbnailFromImageAlways: true,
                kCGImageSourceCreateThumbnailWithTransform: true,
                kCGImageSourceThumbnailMaxPixelSize: maxPixelSize
            ]
            guard let source = CGImageSourceCreateWithURL(url as CFURL, nil),
                  let cgImage = CGImageSourceCreateThumbnailAtIndex(source, 0, options as CFDictionary) else {
                return nil
            }
            let image = NSImage(cgImage: cgImage, size: NSSize(width: cgImage.width, height: cgImage.height))
            return (image, cgImage.bytesPerRow * cgImage.height)
        }
        guard let decoded = await task.value else { return nil }
        let (image, cost) = decoded
        cache.setObject(image, forKey: key(url, maxPixelSize), cost: cost)
        return image
    }

    private func key(_ url: URL, _ size: Int) -> NSString {
        "\(size)|\(url.path)" as NSString
    }
}

struct ThumbnailImage: View {
    let url: URL
    var maxPixelSize = 320
    var contentMode: ContentMode = .fit

    @State private var loaded: NSImage? = nil

    var body: some View {
        // Prefer the cache so the flip-book never flashes; otherwise keep showing the last
        // image until the new one decodes.
        let image = ThumbnailCache.shared.cachedImage(for: url, maxPixelSize: maxPixelSize) ?? loaded
        Group {
            if let image {
                Image(nsImage: image)
                    .resizable()
                    .aspectRatio(contentMode: contentMode)
            } else {
                ProgressView().controlSize(.small)
            }
        }
        .task(id: url) {
            loaded = await ThumbnailCache.shared.image(for: url, maxPixelSize: maxPixelSize)
        }
    }
}
