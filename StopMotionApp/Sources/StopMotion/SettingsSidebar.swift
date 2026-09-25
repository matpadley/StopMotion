import StopMotionKit
import SwiftUI

struct SettingsSidebar: View {
    @EnvironmentObject private var model: ExportModel

    var body: some View {
        VStack(spacing: 0) {
            Form {
                Section("Output") {
                    Picker("Create", selection: $model.settings.outputKind) {
                        ForEach(OutputKind.allCases) { kind in
                            Text(kind.displayName).tag(kind)
                        }
                    }
                    .pickerStyle(.radioGroup)

                    Picker("Resolution", selection: $model.settings.resolution) {
                        ForEach(Resolution.allCases) { resolution in
                            Text(resolution.displayName).tag(resolution)
                        }
                    }

                    if model.settings.outputKind.includesVideo {
                        Picker("Codec", selection: $model.settings.codec) {
                            ForEach(VideoCodec.allCases) { codec in
                                Text(codec.displayName).tag(codec)
                            }
                        }
                    }
                }

                Section {
                    Picker("Frame rate", selection: $model.settings.frameRate) {
                        ForEach(model.frameRates, id: \.self) { rate in
                            Text("\(rate) fps").tag(rate)
                        }
                    }

                    Stepper(value: framesPerImage, in: 1...(model.settings.frameRate * 60)) {
                        LabeledContent("Hold each image") {
                            Text(frameDescription(model.settings.timing.framesPerImage))
                                .monospacedDigit()
                        }
                    }

                    Stepper(value: crossfadeFrames, in: 0...max(0, model.settings.timing.framesPerImage - 1)) {
                        LabeledContent("Crossfade") {
                            Text(model.settings.timing.crossfadeFrames == 0 ? "Cut" : frameDescription(model.settings.timing.crossfadeFrames))
                                .monospacedDigit()
                        }
                    }
                } header: {
                    HStack {
                        Text("Timing")
                        Spacer()
                        Menu("Presets") {
                            Button("Stop motion (12 images/sec, on twos)") { model.applyStopMotionPreset() }
                            Button("Slideshow (2s per image, 0.5s crossfade)") { model.applySlideshowPreset() }
                        }
                        .menuStyle(.borderlessButton)
                        .fixedSize()
                    }
                }

                Section("Image") {
                    Toggle("Auto colour balance", isOn: $model.settings.colorBalance)
                    Text("Neutralises colour casts between shots (gray-world white balance). Images are fitted to the frame with black bars.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }

                Section("Save") {
                    TextField("Name", text: $model.outputName)
                    LabeledContent("Folder") {
                        HStack {
                            Text(model.effectiveOutputFolder?.lastPathComponent ?? "Same as images")
                                .lineLimit(1)
                                .truncationMode(.middle)
                            Button("Change…") { model.chooseOutputFolder() }
                        }
                    }
                    if model.settings.outputKind.includesFinalCutPro {
                        Text("In Final Cut Pro choose File ▸ Import ▸ XML… and pick the .fcpxml file. Stills are placed on the timeline with Cross Dissolves.")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                    }
                }
            }
            .formStyle(.grouped)
            .disabled(model.isExporting)

            Divider()
            VStack(spacing: 6) {
                if !model.images.isEmpty {
                    Text(model.durationDescription)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                Button {
                    model.startExport()
                } label: {
                    Text(exportTitle).frame(maxWidth: .infinity)
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .disabled(!model.canExport)
            }
            .padding()
        }
    }

    private var exportTitle: String {
        switch model.settings.outputKind {
        case .video: return "Export Video"
        case .finalCutPro: return "Export Final Cut Pro Project"
        case .both: return "Export Video + Project"
        }
    }

    private var framesPerImage: Binding<Int> {
        Binding {
            model.settings.timing.framesPerImage
        } set: { frames in
            model.settings.secondsPerImage = Double(frames) / Double(model.settings.frameRate)
        }
    }

    private var crossfadeFrames: Binding<Int> {
        Binding {
            model.settings.timing.crossfadeFrames
        } set: { frames in
            model.settings.crossfadeSeconds = Double(frames) / Double(model.settings.frameRate)
        }
    }

    private func frameDescription(_ frames: Int) -> String {
        let seconds = Double(frames) / Double(model.settings.frameRate)
        let unit = frames == 1 ? "frame" : "frames"
        return "\(frames) \(unit) (\(seconds.formatted(.number.precision(.fractionLength(0...2))))s)"
    }
}
