using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace ImgConcat.Tests
{
    public class ImgProcessingTests
    {
        private Image<Rgb24> CreateSolidColorImage(int width, int height, Rgb24 color)
        {
            var image = new Image<Rgb24>(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    image[x, y] = color;
                }
            }
            return image;
        }

        [Test]
        public void ApplyGrayWorldColorBalance_ShouldReturnImageOfSameSize()
        {
            var service = new ImageProcessingService(NullLogger<ImageProcessingService>.Instance);
            using var input = CreateSolidColorImage(100, 100, new Rgb24(200, 100, 50));
            var method = service.GetType()
                .GetMethod("ApplyGrayWorldColorBalance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "ApplyGrayWorldColorBalance method should exist");
            using var balanced = method!.Invoke(service, new object[] { input }) as Image;
            Assert.That(balanced, Is.Not.Null);
            Assert.That(balanced!.Width, Is.EqualTo(input.Width));
            Assert.That(balanced.Height, Is.EqualTo(input.Height));
        }

        [Test]
        public void ResizeImageToFit_ShouldResizeToTargetDimensions()
        {
            var service = new ImageProcessingService(NullLogger<ImageProcessingService>.Instance);
            using var input = CreateSolidColorImage(300, 150, new Rgb24(100, 200, 50));
            var method = service.GetType().GetMethod("ResizeImageToFit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "ResizeImageToFit method should exist");
            using var resized = method!.Invoke(service, new object[] { input, 1920, 1080 }) as Image;
            Assert.That(resized, Is.Not.Null);
            Assert.That(resized!.Width, Is.EqualTo(1920));
            Assert.That(resized.Height, Is.EqualTo(1080));
        }

        [Test]
        public void BlendImages_ShouldReturnImageOfSameSize()
        {
            var service = new ImageProcessingService(NullLogger<ImageProcessingService>.Instance);
            using var imgA = CreateSolidColorImage(200, 200, new Rgb24(255, 0, 0));
            using var imgB = CreateSolidColorImage(200, 200, new Rgb24(0, 0, 255));
            var method = service.GetType().GetMethod("BlendImages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "BlendImages method should exist");
            using var blended = method!.Invoke(service, new object[] { imgA, imgB, 0.5f }) as Image;
            Assert.That(blended, Is.Not.Null);
            Assert.That(blended!.Width, Is.EqualTo(imgA.Width));
            Assert.That(blended.Height, Is.EqualTo(imgA.Height));
        }
    }
}
