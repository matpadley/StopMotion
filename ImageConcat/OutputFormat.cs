namespace ImgConcat
{
    /// <summary>
    /// What the slideshow generator should produce.
    /// </summary>
    public enum OutputFormat
    {
        /// <summary>An H.264 MP4 video rendered with FFmpeg.</summary>
        Video,

        /// <summary>A Final Cut Pro project (FCPXML) referencing colour-balanced stills.</summary>
        FinalCutPro,

        /// <summary>Both the MP4 video and the Final Cut Pro project.</summary>
        Both
    }
}
