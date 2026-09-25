# StopMotion

Turn a folder of photos into a stop-motion film or slideshow — as a finished **video** or as a
**Final Cut Pro project** you can keep editing.

There are two ways to use it:

| | [StopMotion for Mac](#stopmotion-for-mac-swiftui-app) | [Command line (.NET)](#command-line-tool-net) |
|---|---|---|
| Platform | macOS 14+ | macOS, Windows, Linux |
| Video | H.264 / HEVC MP4 or ProRes 422 MOV (AVFoundation) | H.264 MP4 (FFmpeg) |
| Final Cut Pro project | ✅ `.fcpxml` | ✅ `.fcpxml` |
| Live preview | ✅ flip-book at export timing | – |

## Final Cut Pro projects

Choosing the Final Cut Pro output writes:

- `<name>.fcpxml` — an FCPXML 1.10 project (Final Cut Pro 10.6 or later) with every image on the
  primary storyline for the chosen hold time and, optionally, a **Cross Dissolve** centred on each cut.
- `<name> Media/` — colour-balanced JPEG copies of your images at full resolution, which the
  project points at. (In the app you can turn colour balance off to reference the originals instead.)

In Final Cut Pro choose **File ▸ Import ▸ XML…** and pick the `.fcpxml` file (or double-click it).
A "StopMotion" event is created containing the project, ready to add titles, music and grading.

## StopMotion for Mac (SwiftUI app)

The app lives in [`StopMotionApp/`](StopMotionApp) as a Swift package:

- `StopMotionKit` — Core Image + AVFoundation rendering and the FCPXML writer
- `StopMotion` — the SwiftUI app

### Run it

Requires macOS 14 (Sonoma) or later and Xcode 15 or later.

```bash
cd StopMotionApp
open Package.swift          # opens in Xcode – pick the "StopMotion" scheme and press ⌘R
# or
swift run StopMotion
# or build a double-clickable app at StopMotionApp/build/StopMotion.app
scripts/build-app.sh --open
```

### Using it

1. **Open Folder…** (⌘O) or drag a folder / a selection of images onto the window. Frames are
   ordered like Finder sorts names, so `IMG_2` comes before `IMG_10`. Right-click a frame to leave it out.
2. Pick what to create: **Video**, **Final Cut Pro Project**, or both.
3. Set the timing — frame rate, how many frames to hold each image, and whether to cross dissolve
   between images (and for how long) — or
   use **Presets ▸ Stop motion** (24 fps, each image held 2 frames, hard cuts) or **Slideshow**.
   The preview plays the frames at exactly that timing.
4. Choose resolution (720p, 1080p, 4K), codec and whether to auto colour-balance.
5. **Export** (⌘E). When it finishes you can play the video in the window, show the files in
   Finder, or open the project straight in Final Cut Pro.

## Command line tool (.NET)

A .NET console application that creates MP4 slideshows and/or Final Cut Pro projects from images in a specified directory.

### Features

- Processes various image formats (JPEG, JPG, PNG, BMP, GIF)
- Automatically resizes images to fit 1920x1080 resolution while maintaining aspect ratio
- Creates smooth slideshow with configurable slide duration
- Generates output filename with current date: `new_slide_show-YYYY-MM-DD.mp4`
- Handles images of different sizes by centering them on black backgrounds
- Optionally writes a Final Cut Pro project (`--format fcpxml`) or both (`--format both`)

### Prerequisites

1. **.NET 8.0 SDK** - Download from [Microsoft .NET](https://dotnet.microsoft.com/download)
2. **FFmpeg** - Required for video creation
   - **macOS**: `brew install ffmpeg`
   - **Windows**: Download from [FFmpeg.org](https://ffmpeg.org/download.html)
   - **Linux**: `sudo apt-get install ffmpeg` (Ubuntu/Debian)

### Setup

1. Clone or download this repository
2. Navigate to the project directory
3. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

### Usage

#### Method 1: Command Line Argument
```bash
dotnet run "/path/to/your/images/folder"
```

#### Method 2: Interactive Mode
```bash
dotnet run
```
Then enter the directory path, durations and output format when prompted.

#### Choosing the output

```bash
# MP4 only (default)
dotnet run "/path/to/images" 2.0 0.5
# Final Cut Pro project only
dotnet run "/path/to/images" 2.0 0.5 --format fcpxml
# Both
dotnet run "/path/to/images" --format both
```

`--format` can appear anywhere in the arguments and accepts `video`, `fcpxml` or `both`.
Add `--no-crossfade` for hard cuts between images (the same as a crossfade duration of `0`).
FFmpeg is only needed when a video is produced.

#### Method 3: Build and Run Executable
```bash
dotnet build -c Release
./bin/Release/net8.0/ImgConcat "/path/to/your/images/folder"
```

### Configuration

You can modify the following constants in `Program.cs`:

- `SlideShowWidth`: Video width (default: 1920)
- `SlideShowHeight`: Video height (default: 1080)
- `SlideDurationSeconds`: Duration each image is displayed (default: 2.0 seconds)
- `FrameRate`: Video frame rate (default: 30 FPS)

### Output

The application writes to the same directory as the input images:

- Video: `new_slide_show-YYYY-MM-DD.mp4`
- Final Cut Pro: `new_slide_show-YYYY-MM-DD.fcpxml` plus `new_slide_show-YYYY-MM-DD Media/` containing colour-balanced stills

### Supported Image Formats

- JPEG (.jpg, .jpeg)
- PNG (.png)
- BMP (.bmp)
- GIF (.gif)

### Error Handling

- Validates input directory exists
- Skips corrupted or unsupported image files with warnings
- Provides clear error messages for common issues
- Cleans up temporary files automatically

### Dependencies

- **SixLabors.ImageSharp**: For image processing and manipulation
- **FFMpegCore**: For video creation from image frames
- **System.Drawing.Common**: For additional image format support

## Troubleshooting

1. **FFmpeg not found**: Ensure FFmpeg is installed and available in your system PATH
2. **Permission errors**: Make sure you have read access to the input directory and write access to the output location
3. **Memory issues**: Large numbers of high-resolution images may require more memory

## Acknowledgments

This application was developed using GitHub Copilot AI assistant for code generation and implementation.
