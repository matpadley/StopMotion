# Image Slideshow Generator

A .NET Core console application that creates MP4 slideshows from images in a specified directory.

## Features

- Processes various image formats (JPEG, JPG, PNG, BMP, GIF)
- Automatically resizes images to fit 1920x1080 resolution while maintaining aspect ratio
- Creates smooth slideshow with configurable slide duration
- Generates output filename with current date: `new_slide_show-YYYY-MM-DD.mp4`
- Handles images of different sizes by centering them on black backgrounds

## Prerequisites

1. **.NET 8.0 SDK** - Download from [Microsoft .NET](https://dotnet.microsoft.com/download)
2. **FFmpeg** - Required for video creation
   - **macOS**: `brew install ffmpeg`
   - **Windows**: Download from [FFmpeg.org](https://ffmpeg.org/download.html)
   - **Linux**: `sudo apt-get install ffmpeg` (Ubuntu/Debian)

## Setup

1. Clone or download this repository
2. Navigate to the project directory
3. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

## Usage

### Method 1: Command Line Argument
```bash
dotnet run "/path/to/your/images/folder"
```

### Method 2: Interactive Mode
```bash
dotnet run
```
Then enter the directory path when prompted.

### Method 3: Build and Run Executable
```bash
dotnet build -c Release
./bin/Release/net8.0/ImgConcat "/path/to/your/images/folder"
```

## Configuration

You can modify the following constants in `Program.cs`:

- `SlideShowWidth`: Video width (default: 1920)
- `SlideShowHeight`: Video height (default: 1080)
- `SlideDurationSeconds`: Duration each image is displayed (default: 2.0 seconds)
- `FrameRate`: Video frame rate (default: 30 FPS)

## Output

The application will create an MP4 file named `new_slide_show-YYYY-MM-DD.mp4` in the same directory as the input images.

## Supported Image Formats

- JPEG (.jpg, .jpeg)
- PNG (.png)
- BMP (.bmp)
- GIF (.gif)

## Error Handling

- Validates input directory exists
- Skips corrupted or unsupported image files with warnings
- Provides clear error messages for common issues
- Cleans up temporary files automatically

## Dependencies

- **SixLabors.ImageSharp**: For image processing and manipulation
- **FFMpegCore**: For video creation from image frames
- **System.Drawing.Common**: For additional image format support

## Troubleshooting

1. **FFmpeg not found**: Ensure FFmpeg is installed and available in your system PATH
2. **Permission errors**: Make sure you have read access to the input directory and write access to the output location
3. **Memory issues**: Large numbers of high-resolution images may require more memory
