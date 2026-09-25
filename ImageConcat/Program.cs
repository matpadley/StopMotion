// Add this for service usage
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ImgConcat
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            // Configure logging (console by default). Levels can be controlled via appsettings.json
            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            });

            // Register services
            builder.Services.AddSingleton<IImageProcessingService, ImageProcessingService>();

            using var host = builder.Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            logger.LogInformation("Image Slideshow Generator with Crossfade");
            logger.LogInformation("========================================");
            logger.LogInformation("Usage: ImgConcat <directory> [slide_duration] [crossfade_duration] [--format video|fcpxml|both] [--no-crossfade]");
            logger.LogInformation("  directory: Path to directory containing images");
            logger.LogInformation("  slide_duration: Duration of each slide in seconds (default: 2.0)");
            logger.LogInformation("  crossfade_duration: Duration of crossfade transition in seconds (default: 0.5)");
            logger.LogInformation("  --format: video (MP4, default), fcpxml (Final Cut Pro project) or both");
            logger.LogInformation("  --no-crossfade: hard cuts between images instead of a cross dissolve");

            string inputDirectory;
            double slideDurationSeconds = 2.0; // Default value
            double crossfadeDurationSeconds = 0.5; // Default value
            var outputFormat = OutputFormat.Video; // Default value
            var crossfadeEnabled = true;

            // Pull out the optional --format flag so the remaining arguments stay positional
            var positionalArgs = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is "--format" or "-f")
                {
                    if (i + 1 >= args.Length || !OutputFormatParser.TryParse(args[i + 1], out outputFormat))
                    {
                        logger.LogError("Invalid or missing value for {Flag}. Expected video, fcpxml or both.", args[i]);
                        return 1;
                    }
                    i++;
                }
                else if (args[i] == "--no-crossfade")
                {
                    crossfadeEnabled = false;
                }
                else if (args[i].StartsWith("--format=", StringComparison.Ordinal))
                {
                    var value = args[i]["--format=".Length..];
                    if (!OutputFormatParser.TryParse(value, out outputFormat))
                    {
                        logger.LogError("Invalid format '{Provided}'. Expected video, fcpxml or both.", value);
                        return 1;
                    }
                }
                else
                {
                    positionalArgs.Add(args[i]);
                }
            }
            args = positionalArgs.ToArray();

            if (args.Length > 0)
            {
                inputDirectory = args[0];

                // Parse slide duration if provided
                if (args.Length > 1)
                {
                    if (!double.TryParse(args[1], out slideDurationSeconds) || slideDurationSeconds <= 0)
                    {
                        logger.LogWarning("Invalid slide duration '{Provided}'. Using default value of 2.0 seconds.", args[1]);
                        slideDurationSeconds = 2.0;
                    }
                }

                // Parse crossfade duration if provided
                if (args.Length > 2)
                {
                    if (!double.TryParse(args[2], out crossfadeDurationSeconds) || crossfadeDurationSeconds < 0)
                    {
                        logger.LogWarning("Invalid crossfade duration '{Provided}'. Using default value of 0.5 seconds.", args[2]);
                        crossfadeDurationSeconds = 0.5;
                    }

                    // Ensure crossfade duration is not longer than slide duration
                    if (crossfadeDurationSeconds >= slideDurationSeconds)
                    {
                        logger.LogWarning("Crossfade duration {Crossfade}s cannot be greater than or equal to slide duration {Slide}s. Adjusting crossfade to half of slide duration.", crossfadeDurationSeconds, slideDurationSeconds);
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
                        logger.LogWarning("Invalid slide duration '{Provided}'. Using default value of 2.0 seconds.", slideDurationInput);
                        slideDurationSeconds = 2.0;
                    }
                }

                Console.Write($"Enter crossfade duration in seconds, 0 for none (default: {crossfadeDurationSeconds}): ");
                var crossfadeDurationInput = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(crossfadeDurationInput))
                {
                    if (!double.TryParse(crossfadeDurationInput, out crossfadeDurationSeconds) || crossfadeDurationSeconds < 0)
                    {
                        logger.LogWarning("Invalid crossfade duration '{Provided}'. Using default value of 0.5 seconds.", crossfadeDurationInput);
                        crossfadeDurationSeconds = 0.5;
                    }

                    // Ensure crossfade duration is not longer than slide duration
                    if (crossfadeDurationSeconds >= slideDurationSeconds)
                    {
                        logger.LogWarning("Crossfade duration {Crossfade}s cannot be greater than or equal to slide duration {Slide}s. Adjusting crossfade to half of slide duration.", crossfadeDurationInput, slideDurationSeconds);
                        crossfadeDurationSeconds = slideDurationSeconds / 2;
                    }
                }

                Console.Write("Output format - video, fcpxml or both (default: video): ");
                var formatInput = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(formatInput) && !OutputFormatParser.TryParse(formatInput, out outputFormat))
                {
                    logger.LogWarning("Invalid output format '{Provided}'. Using video.", formatInput);
                    outputFormat = OutputFormat.Video;
                }
            }

            if (!crossfadeEnabled)
            {
                crossfadeDurationSeconds = 0;
            }

            if (string.IsNullOrWhiteSpace(inputDirectory))
            {
                logger.LogError("No directory path provided.");
                return 1;
            }

            if (!Directory.Exists(inputDirectory))
            {
                logger.LogError("Directory '{Directory}' does not exist.", inputDirectory);
                return 1;
            }

            logger.LogInformation("Settings: Slide Duration: {SlideDuration}s, Crossfade Duration: {CrossfadeDuration}s, Output: {OutputFormat}", slideDurationSeconds, crossfadeDurationSeconds, outputFormat);

            // Graceful cancellation (Ctrl+C)
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                logger.LogInformation("Cancellation requested. Attempting to stop gracefully...");
            };

            var imageService = host.Services.GetRequiredService<IImageProcessingService>();
            try
            {
                await imageService.CreateSlideshowAsync(inputDirectory, slideDurationSeconds, crossfadeDurationSeconds, outputFormat, cts.Token);
                logger.LogInformation("Completed successfully.");
                return 0;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Operation canceled by user.");
                return 2;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating slideshow: {Message}", ex.Message);
                return 3;
            }
        }
    }
}
