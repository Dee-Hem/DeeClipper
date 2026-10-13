using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using DeeClipper.Core.Common;

namespace DeeClipper.App.Services;

public static class ImageStorageHelper
{
    public static (string imagePath, string thumbPath, long byteSize, string hash)? SaveClipboardImage(
        Image image,
        DatabasePaths paths,
        long maxSizeBytes)
    {
        try
        {
            paths.EnsureDirectoriesExist();

            using var ms = new MemoryStream();
            image.Save(ms, ImageFormat.Png);
            var imageBytes = ms.ToArray();

            if (imageBytes.LongLength > maxSizeBytes)
            {
                Logger.LogWarning($"Image ignored: size {imageBytes.Length} exceeds limit {maxSizeBytes}");
                return null;
            }

            var hash = HashHelper.ComputeSha256(imageBytes);
            var imageFileName = $"{hash}.png";
            var thumbFileName = $"{hash}_thumb.png";

            var fullImagePath = Path.Combine(paths.ImagesDirectory, imageFileName);
            var fullThumbPath = Path.Combine(paths.ImagesDirectory, thumbFileName);

            if (!File.Exists(fullImagePath))
            {
                File.WriteAllBytes(fullImagePath, imageBytes);
            }

            if (!File.Exists(fullThumbPath))
            {
                CreateThumbnail(image, fullThumbPath, 256);
            }

            return (fullImagePath, fullThumbPath, imageBytes.LongLength, hash);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to save clipboard image", ex);
            return null;
        }
    }

    private static void CreateThumbnail(Image original, string thumbPath, int maxDimension)
    {
        var ratioX = (double)maxDimension / original.Width;
        var ratioY = (double)maxDimension / original.Height;
        var ratio = Math.Min(ratioX, ratioY);
        if (ratio > 1.0) ratio = 1.0;

        var newWidth = Math.Max(1, (int)(original.Width * ratio));
        var newHeight = Math.Max(1, (int)(original.Height * ratio));

        using var thumbBmp = new Bitmap(newWidth, newHeight);
        using (var g = Graphics.FromImage(thumbBmp))
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(original, 0, 0, newWidth, newHeight);
        }

        thumbBmp.Save(thumbPath, ImageFormat.Png);
    }
}
