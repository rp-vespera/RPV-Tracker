using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace RPV_Tracker.Domains.TimeTracking.Services
{
    /// <summary>Captures a screen region — the full virtual desktop or a single monitor — to a JPEG on disk.</summary>
    internal static class ScreenshotService
    {
        // JPEG rather than PNG: a full multi-monitor PNG can run to several megabytes, which
        // adds up fast at one capture per interval. 70 stays clearly legible at a fraction of the size.
        private const long JpegQuality = 70L;

        private static readonly ImageCodecInfo JpegEncoder = FindEncoder(ImageFormat.Jpeg);

        /// <summary>
        /// Captures <paramref name="bounds"/> (the virtual screen, or a single monitor per
        /// the user's capture settings) into <paramref name="folder"/> and returns the file
        /// path. The folder is created if needed. Throws on failure so the caller can record
        /// the error.
        /// </summary>
        public static string Capture(string folder, Rectangle bounds)
        {
            Directory.CreateDirectory(folder);

            // Milliseconds in the name so a capture on manual stop can't collide with the
            // interval capture that may land in the same second.
            string path = Path.Combine(folder, "shot-" + DateTime.Now.ToString("HHmmss-fff") + ".jpg");

            using (var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                }

                Save(bitmap, path);
            }

            return path;
        }

        private static void Save(Bitmap bitmap, string path)
        {
            if (JpegEncoder == null)
            {
                bitmap.Save(path, ImageFormat.Jpeg);
                return;
            }

            using (var parameters = new EncoderParameters(1))
            {
                parameters.Param[0] = new EncoderParameter(Encoder.Quality, JpegQuality);
                bitmap.Save(path, JpegEncoder, parameters);
            }
        }

        private static ImageCodecInfo FindEncoder(ImageFormat format)
        {
            foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
    }
}
