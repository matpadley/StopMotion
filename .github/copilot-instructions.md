# AI Coding Agent Guide for ImgConcat

Purpose: Help you quickly contribute to a .NET 8 console app that builds MP4 slideshows from images with crossfade transitions.

Big picture architecture
- Entry point: `ImageConcat/Program.cs` uses .NET Generic Host (DI + console logging) and parses CLI args: `<directory> [slide_duration] [crossfade_duration]`.
- Service boundary: `IImageProcessingService` with one async method `CreateSlideshowAsync(...)` implemented by `ImageProcessingService`.
- Processing pipeline (in `ImageProcessingService`):
  1) Load images from input dir (extensions: .jpg, .jpeg, .png, .bmp, .gif)
  2) Apply gray-world color balance (clone, per-pixel adjust) → `ApplyGrayWorldColorBalance`
  3) Resize/pad to 1920x1080 with black bars → `ResizeImageToFit` (ImageSharp ResizeOptions: Pad)
  4) Emit JPG frames per slide (FPS=30) and linear crossfade frames between slides → `BlendImages`
  5) Compile frames to MP4 via FFMpegCore (libx264, yuv420p) using pattern `frame_%06d.jpg`
- Output: `new_slide_show-YYYY-MM-DD.mp4` in the source images directory.

Key files
- `ImageConcat/Program.cs` – host, logging, CLI parsing, cancellation, service invocation
- `ImageConcat/IImageProcessingService.cs` – service contract
- `ImageConcat/ImageProcessingService.cs` – pipeline, helpers, FFmpeg call
- `ImgConcat.Tests/ImgProcessingTests.cs` – NUnit tests that reflectively call private helpers
- `README.md` – prerequisites and usage

Developer workflows
- Build: `dotnet build` (VS Code task: “build ImgConcat”)
- Test: `dotnet test` (NUnit). Tests access private methods by reflection; keep method names stable.
- Run: `dotnet run <dir> [slide_seconds] [crossfade_seconds]` or run without args for interactive prompts.
- macOS prerequisite: `brew install ffmpeg` (FFMpegCore requires ffmpeg/ffprobe on PATH).

Conventions and patterns
- DI-first: resolve `IImageProcessingService` from host; keep Program thin.
- Async + cancellation: propagate `CancellationToken`; throw `OperationCanceledException` to stop cleanly.
- ImageSharp usage: work on clones, use `using var`/Dispose; prefer `ProcessPixelRows` for performance.
- Resizing: always use `ResizeMode.Pad` to preserve aspect ratio with black bars.
- Frames: constants in service (`SlideShowWidth=1920`, `SlideShowHeight=1080`, `FrameRate=30`). Temp dir is created under system temp and cleaned after.
- Crossfade math: `framesPerSlide = slideSeconds * FPS`; `crossfadeFrames = crossfadeSeconds * FPS`; normal frames per slide are `framesPerSlide - crossfadeFrames`; blend ratio progresses linearly [0..1]. Program ensures crossfade < slide.
- Logging: use `ILogger<T>`; keep messages structured (named placeholders) and informative.

Extending the pipeline
- Add new image adjustments as private helpers (e.g., vignette, sharpen) and call them before `ResizeImageToFit`.
- Mirror the testing approach: add NUnit tests that create solid images and invoke helpers via reflection to assert size/invariants.
- If adding CLI options, parse in `Program.cs`, validate, and keep non-negative/within-bounds semantics consistent with existing args.

FFmpeg integration details
- Frames saved as high-quality JPEGs (`JpegEncoder { Quality = 95 }`).
- Video is built from pattern input at `FrameRate`, codec `libx264`, CRF 21, pixel format `yuv420p` for broad compatibility.

Common pitfalls to avoid
- Forgetting to dispose `Image` objects → memory pressure; always use `using`.
- Changing helper method names/signatures without updating tests that reflect on `ApplyGrayWorldColorBalance`, `ResizeImageToFit`, `BlendImages`.
- Assuming ffmpeg exists on CI/clean environments; document/install it.
