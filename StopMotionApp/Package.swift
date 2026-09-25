// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "StopMotion",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .executable(name: "StopMotion", targets: ["StopMotion"]),
        .library(name: "StopMotionKit", targets: ["StopMotionKit"])
    ],
    targets: [
        // Rendering + FCPXML export, independent of the UI.
        .target(name: "StopMotionKit"),
        // The SwiftUI macOS app.
        .executableTarget(
            name: "StopMotion",
            dependencies: ["StopMotionKit"]
        )
    ]
)
