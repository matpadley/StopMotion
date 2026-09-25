namespace ImgConcat
{
    public interface IImageProcessingService
    {
        Task CreateSlideshowAsync(string inputDirectory, double slideDurationSeconds, double crossfadeDurationSeconds, OutputFormat outputFormat = OutputFormat.Video, CancellationToken cancellationToken = default);
    }
}

