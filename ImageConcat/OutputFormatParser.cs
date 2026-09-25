namespace ImgConcat
{
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
