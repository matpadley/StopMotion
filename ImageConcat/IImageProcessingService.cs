namespace ImgConcat
{
    public interface IImageProcessingService
    {
        Task CreateSlideshowAsync(string inputDirectory, double slideDurationSeconds, double crossfadeDurationSeconds, CancellationToken cancellationToken = default);
    }
}

