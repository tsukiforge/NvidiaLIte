using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace NvidiaCi
{
    public static class ScreenshotHelper
    {
        public static string CaptureScreen()
        {
            string screenshotFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "NvidiaLite",
                "Screenshots");
            Directory.CreateDirectory(screenshotFolder);

            Rectangle bounds = SystemInformation.VirtualScreen;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidOperationException("Windows did not report a valid desktop area.");

            string fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            string fullPath = Path.Combine(screenshotFolder, fileName);
            int collision = 1;
            while (File.Exists(fullPath))
            {
                fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{collision++}.png";
                fullPath = Path.Combine(screenshotFolder, fileName);
            }

            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
            }
            bitmap.Save(fullPath, ImageFormat.Png);
            return fullPath;
        }
    }
}
