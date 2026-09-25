# CLAUDE.md

Guidance for Claude Code when working in this repository. See `.github/copilot-instructions.md` for
architecture details of the .NET tool.

## Repository layout

- `ImageConcat/` – .NET 8 console app (`ImgConcat`) that builds an MP4 and/or a Final Cut Pro
  project (FCPXML) from a folder of images.
- `ImgConcat.Tests/` – NUnit tests for the .NET app.
- `StopMotionApp/` – SwiftUI macOS app (Swift package: `StopMotionKit` library + `StopMotion` app).
  There is no Swift test target; do not add Swift tests.

## Commands

- .NET build/test: `dotnet build ImgConcat.sln` / `dotnet test ImgConcat.Tests/ImgConcat.Tests.csproj`
- Swift (macOS 14+ only): `cd StopMotionApp && swift build`; `scripts/build-app.sh` builds the `.app`.

## C# conventions

- **One type per file.** Every C# file contains exactly one class, record, enum, interface or struct,
  and the file is named after that type (e.g. `FcpxmlOptions` lives in `FcpxmlOptions.cs`).
  No file may declare multiple types — this includes nested types, so move helpers such as
  `Utf8StringWriter` into their own file (as `internal`) instead of nesting them.
- Keep the FCPXML output of `ImageConcat/FcpxmlBuilder.cs` and
  `StopMotionApp/Sources/StopMotionKit/FCPXMLBuilder.swift` in sync.
