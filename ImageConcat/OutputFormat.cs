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

    public static class OutputFormatParser
    {
        public static bool TryParse(string? value, out OutputFormat format)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "video":
                case "mp4":
                    format = OutputFormat.Video;
                    return true;
                case "fcp":
                case "fcpx":
                case "fcpxml":
                case "finalcut":
                case "finalcutpro":
                    format = OutputFormat.FinalCutPro;
                    return true;
                case "both":
                case "all":
                    format = OutputFormat.Both;
                    return true;
                default:
                    format = OutputFormat.Video;
                    return false;
            }
        }

        public static bool IncludesVideo(this OutputFormat format) => format is OutputFormat.Video or OutputFormat.Both;

        public static bool IncludesFinalCutPro(this OutputFormat format) => format is OutputFormat.FinalCutPro or OutputFormat.Both;
    }
}
