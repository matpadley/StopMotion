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
        private const double SlideDurationSeconds = 2.0;
        private const int FrameRate = 30;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Image Slideshow Generator");
            Console.WriteLine("=========================");

            string inputDirectory;

            if (args.Length > 0)
            {
                inputDirectory = args[0];
            }
            else
            {
                Console.Write("Enter the directory path containing images: ");
                inputDirectory = Console.ReadLine() ?? string.Empty;
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

            try
            {
                await CreateSlideshow(inputDirectory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating slideshow: {ex.Message}");
            }
        }

        static async Task CreateSlideshow(string inputDirectory)
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
                await ProcessImagesAsync(imageFiles, tempDir);

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

        static async Task ProcessImagesAsync(string[] imageFiles, string tempDir)
        {
            Console.WriteLine("Processing images...");
            
            int frameIndex = 0;
            var framesPerSlide = (int)(SlideDurationSeconds * FrameRate);

            for (int i = 0; i < imageFiles.Length; i++)
            {
                var imagePath = imageFiles[i];
                Console.WriteLine($"Processing image {i + 1}/{imageFiles.Length}: {Path.GetFileName(imagePath)}");

                try
                {
                    using var image = await Image.LoadAsync(imagePath);
                    
                    // Resize image to fit slideshow dimensions while maintaining aspect ratio
                    var resizedImage = ResizeImageToFit(image, SlideShowWidth, SlideShowHeight);

                    // Create frames for this slide (duplicate the same image for the duration)
                    for (int frame = 0; frame < framesPerSlide; frame++)
                    {
                        var frameFileName = $"frame_{frameIndex:D6}.jpg";
                        var frameePath = Path.Combine(tempDir, frameFileName);
                        
                        await resizedImage.SaveAsJpegAsync(frameePath, new JpegEncoder { Quality = 95 });
                        frameIndex++;
                    }

                    resizedImage.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not process image {imagePath}: {ex.Message}");
                }
            }

            Console.WriteLine($"Generated {frameIndex} frames.");
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
