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
            using var balanced = service.GetType()
                .GetMethod("ApplyGrayWorldColorBalance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(service, new object[] { input }) as Image;
            Assert.NotNull(balanced);
            Assert.AreEqual(input.Width, balanced.Width);
            Assert.AreEqual(input.Height, balanced.Height);
        }

        [Test]
        public void ResizeImageToFit_ShouldResizeToTargetDimensions()
        {
            var service = new ImageProcessingService(NullLogger<ImageProcessingService>.Instance);
            using var input = CreateSolidColorImage(300, 150, new Rgb24(100, 200, 50));
            var method = service.GetType().GetMethod("ResizeImageToFit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            using var resized = method.Invoke(service, new object[] { input, 1920, 1080 }) as Image;
            Assert.NotNull(resized);
            Assert.AreEqual(1920, resized.Width);
            Assert.AreEqual(1080, resized.Height);
        }

        [Test]
        public void BlendImages_ShouldReturnImageOfSameSize()
        {
            var service = new ImageProcessingService(NullLogger<ImageProcessingService>.Instance);
            using var imgA = CreateSolidColorImage(200, 200, new Rgb24(255, 0, 0));
            using var imgB = CreateSolidColorImage(200, 200, new Rgb24(0, 0, 255));
            var method = service.GetType().GetMethod("BlendImages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            using var blended = method.Invoke(service, new object[] { imgA, imgB, 0.5f }) as Image;
            Assert.NotNull(blended);
            Assert.AreEqual(imgA.Width, blended.Width);
            Assert.AreEqual(imgA.Height, blended.Height);
        }
    }
}
