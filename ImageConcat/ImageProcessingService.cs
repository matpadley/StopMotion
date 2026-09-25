using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using FFMpegCore;
using Microsoft.Extensions.Logging;

namespace ImgConcat
{
    public class ImageProcessingService : IImageProcessingService
    {
        private const int SlideShowWidth = 1920;
        private const int SlideShowHeight = 1080;
        private const int FrameRate = 30;
        private readonly ILogger<ImageProcessingService> _logger;

        public ImageProcessingService(ILogger<ImageProcessingService> logger)
        {
            _logger = logger;
        }

        public async Task CreateSlideshowAsync(string inputDirectory, double slideDurationSeconds, double crossfadeDurationSeconds, OutputFormat outputFormat = OutputFormat.Video, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Processing images from: {InputDirectory}", inputDirectory);

            var supportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            var imageFiles = Directory.GetFiles(inputDirectory)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLower()))
                .OrderBy(file => file)
                .ToArray();

            if (imageFiles.Length == 0)
            {
                _logger.LogWarning("No supported image files found in the directory: {InputDirectory}", inputDirectory);
                return;
            }

            _logger.LogInformation("Found {Count} image(s).", imageFiles.Length);

            var todayDate = DateTime.Now.ToString("yyyy-MM-dd");
            var outputName = $"new_slide_show-{todayDate}";

            if (outputFormat.IncludesVideo())
            {
                await CreateVideoAsync(imageFiles, inputDirectory, outputName, slideDurationSeconds, crossfadeDurationSeconds, cancellationToken);
            }

            if (outputFormat.IncludesFinalCutPro())
            {
                await CreateFinalCutProProjectAsync(imageFiles, inputDirectory, outputName, slideDurationSeconds, crossfadeDurationSeconds, cancellationToken);
            }
        }

        private async Task CreateVideoAsync(string[] imageFiles, string outputDirectory, string outputName, double slideDurationSeconds, double crossfadeDurationSeconds, CancellationToken cancellationToken)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "slideshow_frames");
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
            Directory.CreateDirectory(tempDir);

            try
            {
                await ProcessImagesAsync(imageFiles, tempDir, slideDurationSeconds, crossfadeDurationSeconds, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                var outputPath = Path.Combine(outputDirectory, $"{outputName}.mp4");
                await CreateVideoFromFrames(tempDir, outputPath, cancellationToken);
                _logger.LogInformation("Slideshow created successfully: {OutputPath}", outputPath);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to clean up temporary directory {TempDir}", tempDir);
                }
            }
        }

        /// <summary>
        /// Writes colour-balanced copies of the stills to "&lt;name&gt; Media" and an FCPXML project
        /// that places them on a 1920x1080 timeline with Cross Dissolves matching the video output.
        /// Import the .fcpxml via File ▸ Import ▸ XML… in Final Cut Pro.
        /// </summary>
        private async Task CreateFinalCutProProjectAsync(string[] imageFiles, string outputDirectory, string outputName, double slideDurationSeconds, double crossfadeDurationSeconds, CancellationToken cancellationToken)
        {
            var mediaDir = Path.Combine(outputDirectory, $"{outputName} Media");
            Directory.CreateDirectory(mediaDir);

            var stills = new List<FcpxmlStill>(imageFiles.Length);
            for (int i = 0; i < imageFiles.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var imagePath = imageFiles[i];
                _logger.LogInformation("Preparing still {Current}/{Total} for Final Cut Pro: {File}", i + 1, imageFiles.Length, Path.GetFileName(imagePath));
                try
                {
                    using var image = await Image.LoadAsync(imagePath, cancellationToken);
                    image.Mutate(ctx => ctx.AutoOrient());
                    using var balancedImage = ApplyGrayWorldColorBalance(image);
                    var stillPath = Path.Combine(mediaDir, $"{i + 1:D4}_{Path.GetFileNameWithoutExtension(imagePath)}.jpg");
                    await balancedImage.SaveAsJpegAsync(stillPath, new JpegEncoder { Quality = 95 }, cancellationToken);
                    stills.Add(new FcpxmlStill(stillPath, balancedImage.Width, balancedImage.Height));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not process image {ImagePath}", imagePath);
                }
            }

            if (stills.Count == 0)
            {
                throw new InvalidOperationException("No images could be prepared for the Final Cut Pro project.");
            }

            var options = new FcpxmlOptions(
                ProjectName: outputName,
                Width: SlideShowWidth,
                Height: SlideShowHeight,
                FrameRate: FrameRate,
                FramesPerSlide: Math.Max(1, (int)(slideDurationSeconds * FrameRate)),
                CrossfadeFrames: (int)(crossfadeDurationSeconds * FrameRate));

            var projectPath = Path.Combine(outputDirectory, $"{outputName}.fcpxml");
            await File.WriteAllTextAsync(projectPath, FcpxmlBuilder.Build(stills, options), cancellationToken);
            _logger.LogInformation("Final Cut Pro project created: {ProjectPath} (media in {MediaDir})", projectPath, mediaDir);
        }

        private async Task ProcessImagesAsync(string[] imageFiles, string tempDir, double slideDurationSeconds, double crossfadeDurationSeconds, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing images...");
            int frameIndex = 0;
            var framesPerSlide = (int)(slideDurationSeconds * FrameRate);
            var crossfadeFrames = (int)(crossfadeDurationSeconds * FrameRate);
            if (imageFiles.Length == 0)
            {
                _logger.LogWarning("No images to process.");
                return;
            }
            Image? prevResized = null;
            Image? prevBalanced = null;
            for (int i = 0; i < imageFiles.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var imagePath = imageFiles[i];
                _logger.LogInformation("Loading image {Current}/{Total}: {File}", i + 1, imageFiles.Length, Path.GetFileName(imagePath));
                Image? balancedImage = null;
                Image? resizedImage = null;
                try
                {
                    using var image = await Image.LoadAsync(imagePath, cancellationToken);
                    balancedImage = ApplyGrayWorldColorBalance(image);
                    resizedImage = ResizeImageToFit(balancedImage, SlideShowWidth, SlideShowHeight);
                }
                catch (OperationCanceledException)
                {
                    balancedImage?.Dispose();
                    resizedImage?.Dispose();
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not process image {ImagePath}", imagePath);
                    balancedImage?.Dispose();
                    resizedImage?.Dispose();
                    continue;
                }
                if (prevResized == null)
                {
                    prevBalanced = balancedImage;
                    prevResized = resizedImage;
                    continue;
                }
                var normalFrames = Math.Max(0, framesPerSlide - crossfadeFrames);
                for (int frame = 0; frame < normalFrames; frame++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var frameFileName = $"frame_{frameIndex:D6}.jpg";
                    var framePath = Path.Combine(tempDir, frameFileName);
                    await prevResized.SaveAsJpegAsync(framePath, new JpegEncoder { Quality = 95 }, cancellationToken);
                    frameIndex++;
                }
                var crossfadeTasks = Enumerable.Range(0, crossfadeFrames).Select(frame => Task.Run(async () =>
                {
                    var frameFileName = $"frame_{frameIndex + frame:D6}.jpg";
                    var framePath = Path.Combine(tempDir, frameFileName);
                    // If crossfadeFrames == 1, blendRatio is 1.0 (fully next image). Otherwise, blend from 0.0 to 1.0 across frames.
                    float blendRatio;
                    if (crossfadeFrames == 1)
                    {
                        blendRatio = 1.0f;
                    }
                    else
                    {
                        blendRatio = (float)frame / (crossfadeFrames - 1);
                    }
                    using var blendedImage = BlendImages(prevResized, resizedImage, blendRatio);
                    await blendedImage.SaveAsJpegAsync(framePath, new JpegEncoder { Quality = 95 }, cancellationToken);
                }, cancellationToken));
                await Task.WhenAll(crossfadeTasks);
                frameIndex += crossfadeFrames;
                prevBalanced?.Dispose();
                prevResized?.Dispose();
                prevBalanced = balancedImage;
                prevResized = resizedImage;
            }
            if (prevResized != null)
            {
                _logger.LogInformation("Generating frames for last image");
                for (int frame = 0; frame < framesPerSlide; frame++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var frameFileName = $"frame_{frameIndex:D6}.jpg";
                    var framePath = Path.Combine(tempDir, frameFileName);
                    await prevResized.SaveAsJpegAsync(framePath, new JpegEncoder { Quality = 95 }, cancellationToken);
                    frameIndex++;
                }
                prevBalanced?.Dispose();
                prevResized?.Dispose();
            }
            _logger.LogInformation("Generated {FrameCount} frames with crossfades.", frameIndex);
        }

        private Image ApplyGrayWorldColorBalance(Image sourceImage)
        {
            var image = sourceImage.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgb24>();
            double sumR = 0, sumG = 0, sumB = 0;
            int width = image.Width;
            int height = image.Height;
            int total = width * height;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < width; x++)
                    {
                        sumR += row[x].R;
                        sumG += row[x].G;
                        sumB += row[x].B;
                    }
                }
            });

            double avgR = sumR / total;
            double avgG = sumG / total;
            double avgB = sumB / total;
            double avgGray = (avgR + avgG + avgB) / 3.0;

            // Prevent division by zero by ensuring minimum values
            double safeAvgR = Math.Max(avgR, 1.0);
            double safeAvgG = Math.Max(avgG, 1.0);
            double safeAvgB = Math.Max(avgB, 1.0);

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < width; x++)
                    {
                        var pixel = row[x];
                        pixel.R = (byte)Math.Clamp(pixel.R * avgGray / safeAvgR, 0, 255);
                        pixel.G = (byte)Math.Clamp(pixel.G * avgGray / safeAvgG, 0, 255);
                        pixel.B = (byte)Math.Clamp(pixel.B * avgGray / safeAvgB, 0, 255);
                        row[x] = pixel;
                    }
                }
            });

            return image;
        }

        private Image ResizeImageToFit(Image image, int width, int height)
        {
            var clone = image.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Pad,
                PadColor = Color.Black
            }));
            return clone;
        }

        private Image BlendImages(Image imageA, Image imageB, float blendRatio)
        {
            var blended = imageA.Clone(ctx =>
                ctx.DrawImage(imageB, new Point(0, 0), blendRatio));
            return blended;
        }

        private async Task CreateVideoFromFrames(string framesDir, string outputPath, CancellationToken cancellationToken)
        {
            var frameFiles = Directory.GetFiles(framesDir, "frame_*.jpg").OrderBy(f => f).ToArray();
            if (frameFiles.Length == 0)
            {
                throw new InvalidOperationException("No frames found to create video.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Create video using FFMpeg with frame pattern
            await FFMpegArguments
                .FromFileInput(Path.Combine(framesDir, "frame_%06d.jpg"), false, options => options
                    .WithFramerate(FrameRate))
                .OutputToFile(outputPath, true, options => options
                    .WithVideoCodec("libx264")
                    .WithConstantRateFactor(21)
                    .WithFramerate(FrameRate)
                    .WithCustomArgument("-pix_fmt yuv420p"))
                .ProcessAsynchronously();

            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
