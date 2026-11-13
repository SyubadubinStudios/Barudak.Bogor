using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace BarudakBogor.Game.Rendering;

/// <summary>Paints a name board: cream sign-painter letters on a green plate with a gold rule, like the game's own menus.</summary>
internal static class SignPainter
{
    public const int Width = 512;
    public const int Height = 96;

    /// <summary>RGBA pixels of the board, or null when the interface fonts are not available (headless runs).</summary>
    public static unsafe byte[]? Paint(string text)
    {
        try
        {
            FontFamily family = Application.Current?.FindResource("Display") as FontFamily ?? FontFamily.Default;
            Typeface face = new(family);
            double size = 58;
            FormattedText Lay(double points) => new(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, points, new SolidColorBrush(Color.Parse("#F6ECCB")));
            FormattedText letters = Lay(size);
            // Long names are set smaller rather than cut off.
            if (letters.Width > Width - 36)
            {
                size *= (Width - 36) / letters.Width;
                letters = Lay(size);
            }

            using RenderTargetBitmap bitmap = new(new PixelSize(Width, Height), new Vector(96, 96));
            using (DrawingContext context = bitmap.CreateDrawingContext())
            {
                context.FillRectangle(new SolidColorBrush(Color.Parse("#14703D")), new Rect(0, 0, Width, Height));
                context.FillRectangle(new SolidColorBrush(Color.Parse("#F0B63A")), new Rect(0, Height - 9, Width, 9));
                context.FillRectangle(new SolidColorBrush(Color.Parse("#0E4F2B")), new Rect(0, 0, Width, 5));
                context.DrawText(letters, new Point((Width - letters.Width) / 2, ((Height - 9 - letters.Height) / 2) + 2));
            }

            byte[] pixels = new byte[Width * Height * 4];
            fixed (byte* target = pixels)
            {
                bitmap.CopyPixels(new PixelRect(0, 0, Width, Height), (nint)target, pixels.Length, Width * 4);
            }

            // Avalonia hands back BGRA; the engine wants RGBA.
            for (int i = 0; i < pixels.Length; i += 4)
            {
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
                pixels[i + 3] = 255;
            }

            return pixels;
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }
}
