using System.Xml.Linq;
using NUnit.Framework;

namespace ImgConcat.Tests
{
    public class FcpxmlBuilderTests
    {
        private static FcpxmlOptions DefaultOptions(int crossfadeFrames = 15) =>
            new("Test Project", 1920, 1080, 30, 60, crossfadeFrames);

        private static XDocument BuildDocument(IReadOnlyList<FcpxmlStill> stills, FcpxmlOptions options) =>
            XDocument.Parse(FcpxmlBuilder.Build(stills, options));

        private static List<FcpxmlStill> Stills(int count) =>
            Enumerable.Range(1, count)
                .Select(i => new FcpxmlStill(Path.Combine(Path.GetTempPath(), $"frame {i}.jpg"), 4000, 3000))
                .ToList();

        [Test]
        public void Build_ShouldPlaceEachStillOnSpineBackToBack()
        {
            var doc = BuildDocument(Stills(3), DefaultOptions());

            var clips = doc.Descendants("spine").Elements("video").ToList();
            Assert.That(clips, Has.Count.EqualTo(3));
            Assert.That(clips.Select(c => c.Attribute("offset")!.Value), Is.EqualTo(new[] { "0s", "60/30s", "120/30s" }));
            Assert.That(clips.All(c => c.Attribute("duration")!.Value == "60/30s"), Is.True);

            var sequence = doc.Descendants("sequence").Single();
            Assert.That(sequence.Attribute("duration")!.Value, Is.EqualTo("180/30s"));
        }

        [Test]
        public void Build_ShouldCentreCrossDissolveOnEachCut()
        {
            var doc = BuildDocument(Stills(3), DefaultOptions(crossfadeFrames: 15));

            var transitions = doc.Descendants("spine").Elements("transition").ToList();
            Assert.That(transitions, Has.Count.EqualTo(2));
            Assert.That(transitions.Select(t => t.Attribute("offset")!.Value), Is.EqualTo(new[] { "53/30s", "113/30s" }));
            Assert.That(transitions.All(t => t.Attribute("duration")!.Value == "15/30s"), Is.True);

            var effect = doc.Descendants("effect").Single();
            Assert.That(effect.Attribute("uid")!.Value, Is.EqualTo(FcpxmlBuilder.CrossDissolveUid));
            Assert.That(transitions.All(t => t.Element("filter-video")!.Attribute("ref")!.Value == effect.Attribute("id")!.Value), Is.True);
        }

        [Test]
        public void Build_WithoutCrossfade_ShouldOmitTransitions()
        {
            var doc = BuildDocument(Stills(3), DefaultOptions(crossfadeFrames: 0));

            Assert.That(doc.Descendants("transition"), Is.Empty);
            Assert.That(doc.Descendants("effect"), Is.Empty);
        }

        [Test]
        public void Build_ShouldReferenceAssetsWithFileUrlsAndUniqueIds()
        {
            var stills = Stills(2);
            var doc = BuildDocument(stills, DefaultOptions());

            var assets = doc.Descendants("asset").ToList();
            Assert.That(assets, Has.Count.EqualTo(2));
            Assert.That(assets.Select(a => a.Element("media-rep")!.Attribute("src")!.Value),
                Is.EqualTo(stills.Select(s => new Uri(s.Path).AbsoluteUri)));
            Assert.That(assets[0].Element("media-rep")!.Attribute("src")!.Value, Does.StartWith("file://").And.Contain("frame%201.jpg"));

            var ids = doc.Descendants().Attributes("id").Select(a => a.Value).ToList();
            Assert.That(ids, Is.Unique);

            var clipRefs = doc.Descendants("spine").Elements("video").Select(v => v.Attribute("ref")!.Value);
            Assert.That(clipRefs, Is.EqualTo(assets.Select(a => a.Attribute("id")!.Value)));
        }

        [Test]
        public void Build_ShouldShareStillFormatsBySize()
        {
            var stills = new List<FcpxmlStill>
            {
                new("/tmp/a.jpg", 4000, 3000),
                new("/tmp/b.jpg", 4000, 3000),
                new("/tmp/c.jpg", 3000, 4000),
            };
            var doc = BuildDocument(stills, DefaultOptions());

            var stillFormats = doc.Descendants("format").Where(f => f.Attribute("name")!.Value == "FFVideoFormatRateUndefined").ToList();
            Assert.That(stillFormats, Has.Count.EqualTo(2));

            var sequenceFormat = doc.Descendants("format").First();
            Assert.That(sequenceFormat.Attribute("name")!.Value, Is.EqualTo("FFVideoFormat1080p30"));
            Assert.That(sequenceFormat.Attribute("frameDuration")!.Value, Is.EqualTo("1/30s"));
        }

        [Test]
        public void Build_ShouldProduceVersionedDocumentWithDoctype()
        {
            var xml = FcpxmlBuilder.Build(Stills(1), DefaultOptions());

            Assert.That(xml, Does.Contain("<!DOCTYPE fcpxml>"));
            var doc = XDocument.Parse(xml);
            Assert.That(doc.Root!.Name.LocalName, Is.EqualTo("fcpxml"));
            Assert.That(doc.Root.Attribute("version")!.Value, Is.EqualTo(FcpxmlBuilder.Version));
            Assert.That(doc.Descendants("project").Single().Attribute("name")!.Value, Is.EqualTo("Test Project"));
        }

        [Test]
        public void Build_WithNoStills_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => FcpxmlBuilder.Build(new List<FcpxmlStill>(), DefaultOptions()));
        }

        [TestCase("video", OutputFormat.Video)]
        [TestCase("MP4", OutputFormat.Video)]
        [TestCase("fcpxml", OutputFormat.FinalCutPro)]
        [TestCase("fcp", OutputFormat.FinalCutPro)]
        [TestCase("both", OutputFormat.Both)]
        public void OutputFormatParser_ShouldParseKnownValues(string value, OutputFormat expected)
        {
            Assert.That(OutputFormatParser.TryParse(value, out var format), Is.True);
            Assert.That(format, Is.EqualTo(expected));
        }

        [Test]
        public void OutputFormatParser_ShouldRejectUnknownValues()
        {
            Assert.That(OutputFormatParser.TryParse("gif", out _), Is.False);
        }
    }
}
