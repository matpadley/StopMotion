import Foundation

/// Finds the images in a folder in capture order.
public enum ImageLibrary {
    public static let supportedExtensions: Set<String> = [
        "jpg", "jpeg", "png", "bmp", "gif", "heic", "heif", "tif", "tiff"
    ]

    /// Supported images directly inside `folder` (not sub-folders), sorted the way Finder sorts
    /// names, so `IMG_2.jpg` comes before `IMG_10.jpg`.
    public static func images(in folder: URL) throws -> [URL] {
        let contents = try FileManager.default.contentsOfDirectory(
            at: folder,
            includingPropertiesForKeys: [.isRegularFileKey],
            options: [.skipsHiddenFiles]
        )
        return sorted(contents.filter(isSupportedImage))
    }

    public static func isSupportedImage(_ url: URL) -> Bool {
        supportedExtensions.contains(url.pathExtension.lowercased())
    }

    public static func sorted(_ urls: [URL]) -> [URL] {
        urls.sorted { $0.lastPathComponent.localizedStandardCompare($1.lastPathComponent) == .orderedAscending }
    }
}
