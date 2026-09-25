using System.Xml.Linq;

namespace ImgConcat
{
    /// <summary>A still image placed on the Final Cut Pro timeline.</summary>
    public record FcpxmlStill(string Path, int Width, int Height);

    /// <summary>Timeline settings for an FCPXML export. All durations are in whole frames.</summary>
    public record FcpxmlOptions(
        string ProjectName,
        int Width,
        int Height,
        int FrameRate,
        int FramesPerSlide,
        int CrossfadeFrames,
        string EventName = "StopMotion");

    /// <summary>
    /// Builds an FCPXML 1.10 document (Final Cut Pro 10.6 and later) that places each still
    /// on the primary storyline for <see cref="FcpxmlOptions.FramesPerSlide"/> frames, with a
    /// Cross Dissolve centred on every cut when <see cref="FcpxmlOptions.CrossfadeFrames"/> is non-zero.
    /// </summary>
    public static class FcpxmlBuilder
    {
        public const string Version = "1.10";

        // Built-in Final Cut Pro "Cross Dissolve" transition.
        public const string CrossDissolveUid = "FxPlug:4731E73A-8DAC-4113-9A30-AE85B1761265";

        public static string Build(IReadOnlyList<FcpxmlStill> stills, FcpxmlOptions options)
        {
            if (stills.Count == 0)
            {
                throw new ArgumentException("At least one still is required.", nameof(stills));
            }
            if (options.FrameRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Frame rate must be positive.");
            }
            if (options.FramesPerSlide <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Frames per slide must be positive.");
            }

            var fps = options.FrameRate;
            var crossfadeFrames = Math.Clamp(options.CrossfadeFrames, 0, options.FramesPerSlide);
            var resources = new XElement("resources");

            const string sequenceFormatId = "r1";
            resources.Add(new XElement("format",
                new XAttribute("id", sequenceFormatId),
                new XAttribute("name", SequenceFormatName(options.Width, options.Height, fps)),
                new XAttribute("frameDuration", Time(1, fps)),
                new XAttribute("width", options.Width),
                new XAttribute("height", options.Height),
                new XAttribute("colorSpace", "1-1-1 (Rec. 709)")));

            var nextId = 2;
            string? transitionEffectId = null;
            if (crossfadeFrames > 0 && stills.Count > 1)
            {
                transitionEffectId = $"r{nextId++}";
                resources.Add(new XElement("effect",
                    new XAttribute("id", transitionEffectId),
                    new XAttribute("name", "Cross Dissolve"),
                    new XAttribute("uid", CrossDissolveUid)));
            }

            // Stills share a rate-undefined format per distinct size.
            var stillFormats = new Dictionary<(int Width, int Height), string>();
            var assetIds = new List<string>(stills.Count);
            foreach (var still in stills)
            {
                var size = (still.Width, still.Height);
                if (!stillFormats.TryGetValue(size, out var formatId))
                {
                    formatId = $"r{nextId++}";
                    stillFormats[size] = formatId;
                    resources.Add(new XElement("format",
                        new XAttribute("id", formatId),
                        new XAttribute("name", "FFVideoFormatRateUndefined"),
                        new XAttribute("width", still.Width),
                        new XAttribute("height", still.Height),
                        new XAttribute("colorSpace", "1-13-1")));
                }

                var assetId = $"r{nextId++}";
                assetIds.Add(assetId);
                resources.Add(new XElement("asset",
                    new XAttribute("id", assetId),
                    new XAttribute("name", Path.GetFileNameWithoutExtension(still.Path)),
                    new XAttribute("start", "0s"),
                    new XAttribute("duration", "0s"),
                    new XAttribute("hasVideo", "1"),
                    new XAttribute("format", formatId),
                    new XAttribute("videoSources", "1"),
                    new XElement("media-rep",
                        new XAttribute("kind", "original-media"),
                        new XAttribute("src", new Uri(Path.GetFullPath(still.Path)).AbsoluteUri))));
            }

            var spine = new XElement("spine");
            for (int i = 0; i < stills.Count; i++)
            {
                var clipStart = i * options.FramesPerSlide;
                if (i > 0 && transitionEffectId != null)
                {
                    // Centre the dissolve over the cut; stills have unlimited handles.
                    spine.Add(new XElement("transition",
                        new XAttribute("name", "Cross Dissolve"),
                        new XAttribute("offset", Time(clipStart - crossfadeFrames / 2, fps)),
                        new XAttribute("duration", Time(crossfadeFrames, fps)),
                        new XElement("filter-video",
                            new XAttribute("ref", transitionEffectId),
                            new XAttribute("name", "Cross Dissolve"))));
                }

                spine.Add(new XElement("video",
                    new XAttribute("ref", assetIds[i]),
                    new XAttribute("offset", Time(clipStart, fps)),
                    new XAttribute("name", Path.GetFileNameWithoutExtension(stills[i].Path)),
                    new XAttribute("start", "0s"),
                    new XAttribute("duration", Time(options.FramesPerSlide, fps))));
            }

            var totalFrames = stills.Count * options.FramesPerSlide;
            var document = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XDocumentType("fcpxml", null, null, null),
                new XElement("fcpxml",
                    new XAttribute("version", Version),
                    resources,
                    new XElement("library",
                        new XElement("event",
                            new XAttribute("name", options.EventName),
                            new XElement("project",
                                new XAttribute("name", options.ProjectName),
                                new XElement("sequence",
                                    new XAttribute("format", sequenceFormatId),
                                    new XAttribute("duration", Time(totalFrames, fps)),
                                    new XAttribute("tcStart", "0s"),
                                    new XAttribute("tcFormat", "NDF"),
                                    spine))))));

            using var writer = new Utf8StringWriter();
            document.Save(writer);
            // XDocumentType writes "<!DOCTYPE fcpxml >"; match the form Final Cut Pro exports.
            return writer.ToString().Replace("<!DOCTYPE fcpxml >", "<!DOCTYPE fcpxml>");
        }

        /// <summary>Formats a frame count as an FCPXML rational time, e.g. 60 frames at 30fps → "60/30s".</summary>
        public static string Time(int frames, int frameRate) => frames == 0 ? "0s" : $"{frames}/{frameRate}s";

        private static string SequenceFormatName(int width, int height, int fps) => (width, height) switch
        {
            (1920, 1080) => $"FFVideoFormat1080p{fps}",
            (1280, 720) => $"FFVideoFormat720p{fps}",
            _ => $"FFVideoFormat{width}x{height}p{fps}"
        };

        private sealed class Utf8StringWriter : StringWriter
        {
            public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
        }
    }
}
