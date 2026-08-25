using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Hunbjter;

/// <summary>
/// Builds the window/tray icon at runtime so the app never falls back to the generic
/// WinForms icon. Frames are packed into a real multi-size .ico so the 16px tray
/// rendering stays crisp instead of being downscaled from 48px.
/// </summary>
internal static class AppIcon
{
    private static readonly Lazy<Icon> Instance = new(Build, isThreadSafe: true);

    public static Icon Shared => Instance.Value;

    private static Icon Build()
    {
        int[] sizes = [16, 32, 48, 64];
        var frames = sizes.Select(RenderPng).ToArray();

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((short)0);             // reserved
            writer.Write((short)1);             // type: icon
            writer.Write((short)frames.Length);

            var offset = 6 + (16 * frames.Length);
            for (var i = 0; i < frames.Length; i++)
            {
                var size = sizes[i];
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)(size >= 256 ? 0 : size));
                writer.Write((byte)0);          // palette count
                writer.Write((byte)0);          // reserved
                writer.Write((short)1);         // color planes
                writer.Write((short)32);        // bits per pixel
                writer.Write(frames[i].Length);
                writer.Write(offset);
                offset += frames[i].Length;
            }

            foreach (var frame in frames)
            {
                writer.Write(frame);
            }
        }

        stream.Position = 0;
        return new Icon(stream);
    }

    /// <summary>A rounded dark tile with a recording dot — reads clearly even at 16px.</summary>
    private static byte[] RenderPng(int size)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            var tile = new RectangleF(0, 0, size, size);
            var radius = size * 0.24f;
            using (var path = Theme.RoundedPath(tile, radius))
            using (var brush = new LinearGradientBrush(
                       new RectangleF(0, 0, size, size),
                       Theme.SurfaceAlt,
                       Theme.Background,
                       LinearGradientMode.ForwardDiagonal))
            {
                graphics.FillPath(brush, path);
            }

            // Recording dot, generously sized so it survives the 16px frame.
            var dotDiameter = size * 0.44f;
            var dot = new RectangleF(
                (size - dotDiameter) / 2f,
                (size - dotDiameter) / 2f,
                dotDiameter,
                dotDiameter);

            if (size >= 32)
            {
                using var glow = new SolidBrush(Color.FromArgb(60, Theme.Recording));
                var halo = RectangleF.Inflate(dot, dotDiameter * 0.30f, dotDiameter * 0.30f);
                graphics.FillEllipse(glow, halo);
            }

            using (var brush = new SolidBrush(Theme.Recording))
            {
                graphics.FillEllipse(brush, dot);
            }
        }

        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        return png.ToArray();
    }
}
