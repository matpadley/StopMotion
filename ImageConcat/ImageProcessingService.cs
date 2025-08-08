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

        public async Task CreateSlideshowAsync(string inputDirectory, double slideDurationSeconds, double crossfadeDurationSeconds, CancellationToken cancellationToken = default)
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

                var todayDate = DateTime.Now.ToString("yyyy-MM-dd");
                var outputPath = Path.Combine(inputDirectory, $"new_slide_show-{todayDate}.mp4");
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
                    float blendRatio = crossfadeFrames == 1 ? 1.0f : (float)frame / (crossfadeFrames - 1);
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

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < width; x++)
                    {
                        var pixel = row[x];
                        pixel.R = (byte)Math.Clamp(pixel.R * avgGray / avgR, 0, 255);
                        pixel.G = (byte)Math.Clamp(pixel.G * avgGray / avgG, 0, 255);
                        pixel.B = (byte)Math.Clamp(pixel.B * avgGray / avgB, 0, 255);
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
