using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace SwapCtrlAlt
{
    class IconGenerator
    {
        static void Main(string[] args)
        {
            string icoPath = args[0];
            string previewPath = args.Length > 1 ? args[1] : null;

            int[] sizes = new int[] { 256, 64, 48, 32, 16 };
            byte[][] pngBuffers = new byte[sizes.Length][];

            for (int i = 0; i < sizes.Length; i++)
            {
                using (Bitmap bmp = Program.RenderKeycapArt(sizes[i], true))
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    pngBuffers[i] = ms.ToArray();
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(icoPath));
            using (FileStream fs = new FileStream(icoPath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write((short)0);
                bw.Write((short)1);
                bw.Write((short)sizes.Length);

                int offset = 6 + (16 * sizes.Length);
                for (int i = 0; i < sizes.Length; i++)
                {
                    int s = sizes[i];
                    bw.Write((byte)(s >= 256 ? 0 : s));
                    bw.Write((byte)(s >= 256 ? 0 : s));
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((short)1);
                    bw.Write((short)32);
                    bw.Write(pngBuffers[i].Length);
                    bw.Write(offset);
                    offset += pngBuffers[i].Length;
                }

                for (int i = 0; i < sizes.Length; i++)
                {
                    bw.Write(pngBuffers[i]);
                }
            }

            if (!string.IsNullOrEmpty(previewPath))
            {
                RenderPreviewBanner(previewPath);
            }
        }

        private static void RenderPreviewBanner(string path)
        {
            int w = 880, h = 380;
            using (Bitmap bmp = new Bitmap(w, h))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                using (LinearGradientBrush bg = new LinearGradientBrush(
                    new Point(0, 0), new Point(w, h),
                    Color.FromArgb(15, 23, 42), Color.FromArgb(30, 27, 75)))
                {
                    g.FillRectangle(bg, 0, 0, w, h);
                }

                using (Bitmap appIcon = Program.RenderKeycapArt(140, true))
                {
                    g.DrawImage(appIcon, 65, 65, 140, 140);
                }

                using (Font fHero = new Font("Segoe UI", 30f, FontStyle.Bold))
                using (Font fDesc = new Font("Segoe UI", 13f, FontStyle.Regular))
                using (Font fBadge = new Font("Consolas", 11f, FontStyle.Bold))
                using (SolidBrush bWhite = new SolidBrush(Color.White))
                using (SolidBrush bMuted = new SolidBrush(Color.FromArgb(148, 163, 184)))
                using (SolidBrush bCyan = new SolidBrush(Color.FromArgb(56, 189, 248)))
                {
                    g.DrawString("SwapCtrlAlt", fHero, bWhite, new PointF(230, 68));
                    g.DrawString("Tiny ~42 KB standalone utility to swap Left Ctrl ⇄ Left Alt on demand.", fDesc, bMuted, new PointF(234, 128));
                    g.DrawString("Zero dependencies • No Registry changes • Replaces 1.3 GB PowerToys", fBadge, bCyan, new PointF(235, 165));
                }

                // Draw simulated Active Toast & Disabled Toast cards
                DrawToastCard(g, 65, 245, 360, 74,
                    "ACTIVE: Left Ctrl ⇄ Left Alt",
                    "Run the app again or use tray icon to disable",
                    Color.FromArgb(16, 185, 129), true);

                DrawToastCard(g, 455, 245, 360, 74,
                    "DISABLED: Default Keyboard Restored",
                    "Left Ctrl and Left Alt are back to normal",
                    Color.FromArgb(239, 68, 68), false);

                bmp.Save(path, ImageFormat.Png);
            }
        }

        private static void DrawToastCard(Graphics g, int x, int y, int w, int h, string title, string sub, Color accent, bool active)
        {
            RectangleF rect = new RectangleF(x, y, w, h);
            using (GraphicsPath path = Program.RoundedRect(rect, 15f))
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(24, 24, 32)))
            using (Pen border = new Pen(accent, 2f))
            {
                g.FillPath(bg, path);
                g.DrawPath(border, path);
            }

            using (Bitmap icon = Program.RenderKeycapArt(48, active))
            {
                g.DrawImage(icon, x + 16, y + 13, 48, 48);
            }

            using (Font fTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (Font fSub = new Font("Segoe UI", 8.8f, FontStyle.Regular))
            using (SolidBrush bAccent = new SolidBrush(accent))
            using (SolidBrush bSub = new SolidBrush(Color.FromArgb(200, 210, 225)))
            {
                g.DrawString(title, fTitle, bAccent, new PointF(x + 76, y + 15));
                g.DrawString(sub, fSub, bSub, new PointF(x + 76, y + 40));
            }
        }
    }
}
