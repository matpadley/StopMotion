import AppKit
import Foundation
import StopMotionKit

@MainActor
final class ExportModel: ObservableObject {
    enum Phase: Equatable {
        case idle
        case exporting(progress: Double)
        case finished(ExportResult)
        case failed(String)
    }

    @Published private(set) var sourceFolder: URL?
    @Published var images: [URL] = []
    @Published var outputFolder: URL?
    @Published var outputName = StopMotionExporter.defaultName()
    @Published private(set) var phase: Phase = .idle
    @Published var settings: ExportSettings {
        didSet {
            normalizeFrameRate()
            saveSettings()
        }
    }

    private var exportTask: Task<Void, Never>?
    private static let settingsKey = "ExportSettings"

    init() {
        if let data = UserDefaults.standard.data(forKey: Self.settingsKey),
           let saved = try? JSONDecoder().decode(ExportSettings.self, from: data) {
            settings = saved
        } else {
            settings = ExportSettings()
        }
    }

    // MARK: - Derived state

    var isExporting: Bool {
        if case .exporting = phase { return true }
        return false
    }

    var canExport: Bool {
        !images.isEmpty && !isExporting && effectiveOutputFolder != nil && !trimmedName.isEmpty
    }

    var effectiveOutputFolder: URL? { outputFolder ?? sourceFolder }

    var frameRates: [Int] { ExportSettings.frameRates(for: settings.outputKind) }

    var durationDescription: String {
        let timing = settings.timing
        let seconds = timing.duration(imageCount: images.count)
        let formatter = DateComponentsFormatter()
        formatter.allowedUnits = seconds >= 3600 ? [.hour, .minute, .second] : [.minute, .second]
        formatter.zeroFormattingBehavior = .pad
        let clock = formatter.string(from: seconds) ?? "\(Int(seconds))s"
        return "\(images.count) images · \(timing.totalFrames(imageCount: images.count)) frames · \(clock)"
    }

    private var trimmedName: String {
        outputName.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    // MARK: - Source

    func chooseSourceFolder() {
        let panel = NSOpenPanel()
        panel.title = "Choose a folder of frames"
        panel.canChooseDirectories = true
        panel.canChooseFiles = false
        panel.allowsMultipleSelection = false
        if panel.runModal() == .OK, let url = panel.url {
            loadFolder(url)
        }
    }

    func chooseOutputFolder() {
        let panel = NSOpenPanel()
        panel.title = "Choose where to save exports"
        panel.canChooseDirectories = true
        panel.canChooseFiles = false
        panel.canCreateDirectories = true
        panel.allowsMultipleSelection = false
        panel.directoryURL = effectiveOutputFolder
        if panel.runModal() == .OK, let url = panel.url {
            outputFolder = url
        }
    }

    func loadFolder(_ url: URL) {
        do {
            images = try ImageLibrary.images(in: url)
            sourceFolder = url
            phase = images.isEmpty ? .failed("No supported images in \(url.lastPathComponent).") : .idle
        } catch {
            phase = .failed(error.localizedDescription)
        }
    }

    func reloadFolder() {
        if let sourceFolder { loadFolder(sourceFolder) }
    }

    /// Handles a drop of either a folder or a set of image files.
    func handleDrop(_ urls: [URL]) -> Bool {
        if urls.count == 1, urls[0].hasDirectoryPath {
            loadFolder(urls[0])
            return true
        }
        let dropped = ImageLibrary.sorted(urls.filter(ImageLibrary.isSupportedImage))
        guard !dropped.isEmpty else { return false }
        images = dropped
        sourceFolder = dropped[0].deletingLastPathComponent()
        phase = .idle
        return true
    }

    func remove(_ url: URL) {
        images.removeAll { $0 == url }
    }

    // MARK: - Export

    func startExport() {
        guard canExport, let folder = effectiveOutputFolder else { return }
        let images = self.images
        let settings = self.settings
        let name = trimmedName
        phase = .exporting(progress: 0)

        // The model lives for the whole app, so strong captures are fine here.
        exportTask = Task {
            do {
                let result = try await StopMotionExporter().export(images: images, settings: settings, name: name, to: folder) { value in
                    Task { @MainActor in
                        guard case .exporting = self.phase else { return }
                        self.phase = .exporting(progress: value)
                    }
                }
                phase = .finished(result)
            } catch is CancellationError {
                phase = .idle
            } catch {
                phase = .failed(error.localizedDescription)
            }
        }
    }

    func cancelExport() {
        exportTask?.cancel()
    }

    func dismissResult() {
        phase = .idle
    }

    func revealInFinder(_ url: URL) {
        NSWorkspace.shared.activateFileViewerSelecting([url])
    }

    func openInFinalCutPro(_ url: URL) {
        let workspace = NSWorkspace.shared
        if let finalCut = workspace.urlForApplication(withBundleIdentifier: "com.apple.FinalCut") {
            workspace.open([url], withApplicationAt: finalCut, configuration: NSWorkspace.OpenConfiguration())
        } else {
            workspace.open(url)
        }
    }

    var isFinalCutProInstalled: Bool {
        NSWorkspace.shared.urlForApplication(withBundleIdentifier: "com.apple.FinalCut") != nil
    }

    // MARK: - Settings

    func applyStopMotionPreset() {
        var preset = settings
        preset.frameRate = 24
        preset.secondsPerImage = 2.0 / 24.0 // "on twos": 12 drawings a second
        preset.crossfadeSeconds = 0
        settings = preset
    }

    func applySlideshowPreset() {
        var preset = settings
        preset.frameRate = 30
        preset.secondsPerImage = 2.0
        preset.crossfadeSeconds = 0.5
        settings = preset
    }

    private func normalizeFrameRate() {
        let rates = frameRates
        guard !rates.contains(settings.frameRate) else { return }
        // Pick the nearest supported rate, e.g. 12fps → 24fps for Final Cut Pro.
        settings.frameRate = rates.min { abs($0 - settings.frameRate) < abs($1 - settings.frameRate) } ?? 30
    }

    private func saveSettings() {
        if let data = try? JSONEncoder().encode(settings) {
            UserDefaults.standard.set(data, forKey: Self.settingsKey)
        }
    }
}
