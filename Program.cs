using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using FFMpegCore;
using FFMpegCore.Enums;
using System.Globalization;

namespace ImgConcat
{
    class Program
    {
        private const int SlideShowWidth = 1920;
        private const int SlideShowHeight = 1080;
        private const int FrameRate = 30;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Image Slideshow Generator with Crossfade");
            Console.WriteLine("========================================");
            Console.WriteLine("Usage: ImgConcat <directory> [slide_duration] [crossfade_duration]");
            Console.WriteLine("  directory: Path to directory containing images");
            Console.WriteLine("  slide_duration: Duration of each slide in seconds (default: 2.0)");
            Console.WriteLine("  crossfade_duration: Duration of crossfade transition in seconds (default: 0.5)");
            Console.WriteLine();

            string inputDirectory;
            double slideDurationSeconds = 2.0; // Default value
            double crossfadeDurationSeconds = 0.5; // Default value

            if (args.Length > 0)
            {
                inputDirectory = args[0];
                
                // Parse slide duration if provided
                if (args.Length > 1)
                {
                    if (!double.TryParse(args[1], out slideDurationSeconds) || slideDurationSeconds <= 0)
                    {
                        Console.WriteLine("Error: Invalid slide duration. Using default value of 2.0 seconds.");
                        slideDurationSeconds = 2.0;
                    }
                }
                
                // Parse crossfade duration if provided
                if (args.Length > 2)
                {
                    if (!double.TryParse(args[2], out crossfadeDurationSeconds) || crossfadeDurationSeconds < 0)
                    {
                        Console.WriteLine("Error: Invalid crossfade duration. Using default value of 0.5 seconds.");
                        crossfadeDurationSeconds = 0.5;
                    }
                    
                    // Ensure crossfade duration is not longer than slide duration
                    if (crossfadeDurationSeconds >= slideDurationSeconds)
                    {
                        Console.WriteLine("Warning: Crossfade duration cannot be greater than or equal to slide duration. Adjusting crossfade to half of slide duration.");
                        crossfadeDurationSeconds = slideDurationSeconds / 2;
                    }
                }
            }
            else
            {
                Console.Write("Enter the directory path containing images: ");
                inputDirectory = Console.ReadLine() ?? string.Empty;
                
                Console.Write($"Enter slide duration in seconds (default: {slideDurationSeconds}): ");
                var slideDurationInput = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(slideDurationInput))
                {
                    if (!double.TryParse(slideDurationInput, out slideDurationSeconds) || slideDurationSeconds <= 0)
                    {
                        Console.WriteLine("Invalid slide duration. Using default value of 2.0 seconds.");
                        slideDurationSeconds = 2.0;
                    }
                }
                
                Console.Write($"Enter crossfade duration in seconds (default: {crossfadeDurationSeconds}): ");
                var crossfadeDurationInput = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(crossfadeDurationInput))
                {
                    if (!double.TryParse(crossfadeDurationInput, out crossfadeDurationSeconds) || crossfadeDurationSeconds < 0)
                    {
                        Console.WriteLine("Invalid crossfade duration. Using default value of 0.5 seconds.");
                        crossfadeDurationSeconds = 0.5;
                    }
                    
                    // Ensure crossfade duration is not longer than slide duration
                    if (crossfadeDurationSeconds >= slideDurationSeconds)
                    {
                        Console.WriteLine("Warning: Crossfade duration cannot be greater than or equal to slide duration. Adjusting crossfade to half of slide duration.");
                        crossfadeDurationSeconds = slideDurationSeconds / 2;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(inputDirectory))
            {
                Console.WriteLine("Error: No directory path provided.");
                return;
            }

            if (!Directory.Exists(inputDirectory))
            {
                Console.WriteLine($"Error: Directory '{inputDirectory}' does not exist.");
                return;
            }

            Console.WriteLine($"Settings:");
            Console.WriteLine($"  Slide Duration: {slideDurationSeconds} seconds");
            Console.WriteLine($"  Crossfade Duration: {crossfadeDurationSeconds} seconds");
            Console.WriteLine();

            try
            {
                await CreateSlideshow(inputDirectory, slideDurationSeconds, crossfadeDurationSeconds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating slideshow: {ex.Message}");
            }
        }

        static async Task CreateSlideshow(string inputDirectory, double slideDurationSeconds, double crossfadeDurationSeconds)
        {
            Console.WriteLine($"Processing images from: {inputDirectory}");

            // Get all image files
            var supportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            var imageFiles = Directory.GetFiles(inputDirectory)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLower()))
                .OrderBy(file => file)
                .ToArray();

            if (imageFiles.Length == 0)
            {
                Console.WriteLine("No supported image files found in the directory.");
                return;
            }

            Console.WriteLine($"Found {imageFiles.Length} image(s).");

            // Create temporary directory for processed frames
            var tempDir = Path.Combine(Path.GetTempPath(), "slideshow_frames");
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
            Directory.CreateDirectory(tempDir);

            try
            {
                // Process each image and create frames
                await ProcessImagesAsync(imageFiles, tempDir, slideDurationSeconds, crossfadeDurationSeconds);

                // Generate output filename with today's date
                var todayDate = DateTime.Now.ToString("yyyy-MM-dd");
                var outputPath = Path.Combine(inputDirectory, $"new_slide_show-{todayDate}.mp4");

                // Create video from frames
                await CreateVideoFromFrames(tempDir, outputPath);

                Console.WriteLine($"Slideshow created successfully: {outputPath}");
            }
            finally
            {
                // Clean up temporary directory
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        static async Task ProcessImagesAsync(string[] imageFiles, string tempDir, double slideDurationSeconds, double crossfadeDurationSeconds)
        {
            Console.WriteLine("Processing images...");
            
            int frameIndex = 0;
            var framesPerSlide = (int)(slideDurationSeconds * FrameRate);
            var crossfadeFrames = (int)(crossfadeDurationSeconds * FrameRate);

            // Load and resize all images first
            var processedImages = new List<Image>();
            for (int i = 0; i < imageFiles.Length; i++)
            {
                var imagePath = imageFiles[i];
                Console.WriteLine($"Loading image {i + 1}/{imageFiles.Length}: {Path.GetFileName(imagePath)}");

                try
                {
                    using var image = await Image.LoadAsync(imagePath);
                    var resizedImage = ResizeImageToFit(image, SlideShowWidth, SlideShowHeight);
                    processedImages.Add(resizedImage);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not process image {imagePath}: {ex.Message}");
                }
            }

            if (processedImages.Count == 0)
            {
                Console.WriteLine("No images were successfully processed.");
                return;
            }

            // Generate frames with crossfades
            for (int i = 0; i < processedImages.Count; i++)
            {
                var currentImage = processedImages[i];
                Console.WriteLine($"Generating frames for image {i + 1}/{processedImages.Count}");

                // For all images except the last one, create frames with crossfade
                if (i < processedImages.Count - 1)
                {
                    var nextImage = processedImages[i + 1];
                    
                    // Create normal frames for the main duration
                    var normalFrames = framesPerSlide - crossfadeFrames;
                    for (int frame = 0; frame < normalFrames; frame++)
                    {
                        var frameFileName = $"frame_{frameIndex:D6}.jpg";
                        var framePath = Path.Combine(tempDir, frameFileName);
                        
                        await currentImage.SaveAsJpegAsync(framePath, new JpegEncoder { Quality = 95 });
                        frameIndex++;
                    }

                    // Create crossfade frames
                    for (int frame = 0; frame < crossfadeFrames; frame++)
                    {
                        var frameFileName = $"frame_{frameIndex:D6}.jpg";
                        var framePath = Path.Combine(tempDir, frameFileName);
                        
                        // Calculate blend ratio (0 = current image, 1 = next image)
                        float blendRatio = (float)frame / (crossfadeFrames - 1);
                        
                        using var blendedImage = BlendImages(currentImage, nextImage, blendRatio);
                        await blendedImage.SaveAsJpegAsync(framePath, new JpegEncoder { Quality = 95 });
                        frameIndex++;
                    }
                }
                else
                {
                    // For the last image, just create normal frames without crossfade
                    for (int frame = 0; frame < framesPerSlide; frame++)
                    {
                        var frameFileName = $"frame_{frameIndex:D6}.jpg";
                        var framePath = Path.Combine(tempDir, frameFileName);
                        
                        await currentImage.SaveAsJpegAsync(framePath, new JpegEncoder { Quality = 95 });
                        frameIndex++;
                    }
                }
            }

            // Clean up processed images
            foreach (var image in processedImages)
            {
                image.Dispose();
            }

            Console.WriteLine($"Generated {frameIndex} frames with crossfades.");
        }

        static Image ResizeImageToFit(Image sourceImage, int targetWidth, int targetHeight)
        {
            var sourceWidth = sourceImage.Width;
            var sourceHeight = sourceImage.Height;

            // Calculate scaling factor to fit image within target dimensions
            var scaleX = (double)targetWidth / sourceWidth;
            var scaleY = (double)targetHeight / sourceHeight;
            var scale = Math.Min(scaleX, scaleY);

            var newWidth = (int)(sourceWidth * scale);
            var newHeight = (int)(sourceHeight * scale);

            // Create a new image with target dimensions and black background
            var result = new Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(targetWidth, targetHeight);
            
            result.Mutate(ctx =>
            {
                // Fill with black background
                ctx.BackgroundColor(SixLabors.ImageSharp.Color.Black);
                
                // Calculate position to center the resized image
                var x = (targetWidth - newWidth) / 2;
                var y = (targetHeight - newHeight) / 2;
                
                // Create a copy of the source image and resize it
                using var resizedSource = sourceImage.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgb24>();
                resizedSource.Mutate(rCtx => rCtx.Resize(newWidth, newHeight));
                
                // Draw resized image onto the result
                ctx.DrawImage(resizedSource, new Point(x, y), 1.0f);
            });

            return result;
        }

        static Image BlendImages(Image image1, Image image2, float blendRatio)
        {
            // Create a new image for the blended result
            var result = new Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(SlideShowWidth, SlideShowHeight);
            
            result.Mutate(ctx =>
            {
                // Fill with black background
                ctx.BackgroundColor(SixLabors.ImageSharp.Color.Black);
                
                // Draw the first image with reduced opacity
                ctx.DrawImage(image1, new Point(0, 0), 1.0f - blendRatio);
                
                // Draw the second image with increasing opacity
                ctx.DrawImage(image2, new Point(0, 0), blendRatio);
            });

            return result;
        }

        static async Task CreateVideoFromFrames(string framesDir, string outputPath)
        {
            Console.WriteLine("Creating video from frames...");

            var framePattern = Path.Combine(framesDir, "frame_%06d.jpg");
            
            try
            {
                await FFMpegArguments
                    .FromFileInput(framePattern, false, options => options
                        .WithFramerate(FrameRate))
                    .OutputToFile(outputPath, true, options => options
                        .WithVideoCodec(VideoCodec.LibX264)
                        .WithAudioCodec(AudioCodec.Aac)
                        .WithVariableBitrate(4)
                        .WithVideoFilters(filterOptions => filterOptions
                            .Scale(SlideShowWidth, SlideShowHeight))
                        .WithFramerate(FrameRate))
                    .ProcessAsynchronously();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FFmpeg error: {ex.Message}");
                Console.WriteLine("Make sure FFmpeg is installed and available in your PATH.");
                throw;
            }
        }
    }
}
