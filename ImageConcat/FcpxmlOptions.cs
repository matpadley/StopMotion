namespace ImgConcat
{
    /// <summary>Timeline settings for an FCPXML export. All durations are in whole frames.</summary>
    public record FcpxmlOptions(
        string ProjectName,
        int Width,
        int Height,
        int FrameRate,
        int FramesPerSlide,
        int CrossfadeFrames,
        string EventName = "StopMotion");
}
