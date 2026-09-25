import AppKit
import SwiftUI

@main
struct StopMotionApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate
    @StateObject private var model = ExportModel()

    var body: some Scene {
        WindowGroup("StopMotion") {
            ContentView()
                .environmentObject(model)
                .frame(minWidth: 900, minHeight: 600)
        }
        .commands {
            CommandGroup(replacing: .newItem) {
                Button("Open Folder…") { model.chooseSourceFolder() }
                    .keyboardShortcut("o")
            }
            CommandGroup(after: .newItem) {
                Button("Export") { model.startExport() }
                    .keyboardShortcut("e")
                    .disabled(!model.canExport)
            }
        }
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        // Needed when launched with `swift run` rather than from an .app bundle.
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        true
    }
}
