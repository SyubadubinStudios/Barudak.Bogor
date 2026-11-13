using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using BarudakBogor.World;

namespace BarudakBogor.Game.Rendering;

/// <summary>
/// Paints the lettering of the town with the interface's own typeface: the green name boards of
/// the well-known places, cloth banners, the faces of shop light boxes and the pictures on the
/// big LED screens.
/// </summary>
internal static class SignPainter
{
    public const int Width = 512;

    /// <summary>Size of an LED screen picture.</summary>
    public const int ScreenHeight = 288;

    /// <summary>Pixel height of a painted sign: name boards and banners are long strips, a light box is squarer.</summary>
    public static int HeightOf(SignLook look) => look == SignLook.LightBox ? 160 : 96;

    /// <summary>RGBA pixels of the sign, or null when the interface fonts are not available (headless runs).</summary>
    public static byte[]? Paint(string text, SignLook look = SignLook.Board)
    {
        int height = HeightOf(look);
        (string ground, string ink, string? rule, string? top) = look switch
        {
            SignLook.Banner => ("#F4F1E6", "#B8231C", null, null),
            SignLook.LightBox => ("#FFF4CF", "#1D3F8F", "#D2281E", null),
            _ => ("#14703D", "#F6ECCB", "#F0B63A", "#0E4F2B"),
        };
        return Render(Width, height, context =>
        {
            context.FillRectangle(new SolidColorBrush(Color.Parse(ground)), new Rect(0, 0, Width, height));
            int foot = 0;
            if (rule is not null)
            {
                foot = look == SignLook.LightBox ? 16 : 9;
                context.FillRectangle(new SolidColorBrush(Color.Parse(rule)), new Rect(0, height - foot, Width, foot));
            }

            if (top is not null)
            {
                context.FillRectangle(new SolidColorBrush(Color.Parse(top)), new Rect(0, 0, Width, 5));
            }

            FormattedText letters = Fit(text, look == SignLook.LightBox ? 74 : 58, Width - 36, ink);
            context.DrawText(letters, new Point((Width - letters.Width) / 2, ((height - foot - letters.Height) / 2) + 2));
        });
    }

    /// <summary>A picture for the LED screen: a headline, a line under it, in one of a few loud colour schemes.</summary>
    public static byte[]? PaintScreen(string headline, string line, int scheme)
    {
        (string ground, string band, string ink, string small) = (scheme % 4) switch
        {
            0 => ("#C7361F", "#F6C12C", "#FFF6DC", "#3A1208"),
            1 => ("#F2B01E", "#1F6F3D", "#3A1208", "#FFF6DC"),
            2 => ("#1B4E9B", "#4FC3E8", "#FFFFFF", "#0C2346"),
            _ => ("#15803D", "#C9E62E", "#FFFFFF", "#0B3D1E"),
        };
        return Render(Width, ScreenHeight, context =>
        {
            context.FillRectangle(new SolidColorBrush(Color.Parse(ground)), new Rect(0, 0, Width, ScreenHeight));
            context.FillRectangle(new SolidColorBrush(Color.Parse(band)), new Rect(0, ScreenHeight - 92, Width, 92));
            context.FillRectangle(new SolidColorBrush(Color.Parse(band)), new Rect(0, 0, Width, 14));
            FormattedText big = Fit(headline.ToUpperInvariant(), 104, Width - 40, ink);
            context.DrawText(big, new Point((Width - big.Width) / 2, 14 + ((ScreenHeight - 106 - big.Height) / 2)));
            FormattedText little = Fit(line, 44, Width - 40, small);
            context.DrawText(little, new Point((Width - little.Width) / 2, ScreenHeight - 92 + ((92 - little.Height) / 2)));
        });
    }

    /// <summary>Lays out a line of lettering; long ones are set smaller rather than cut off.</summary>
    private static FormattedText Fit(string text, double size, double room, string colour)
    {
        FontFamily family = Application.Current?.FindResource("Display") as FontFamily ?? FontFamily.Default;
        Typeface face = new(family);
        FormattedText Lay(double points) => new(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, points, new SolidColorBrush(Color.Parse(colour)));
        FormattedText letters = Lay(size);
        return letters.Width > room ? Lay(size * room / letters.Width) : letters;
    }

    private static unsafe byte[]? Render(int width, int height, Action<DrawingContext> draw)
    {
        try
        {
            using RenderTargetBitmap bitmap = new(new PixelSize(width, height), new Vector(96, 96));
            using (DrawingContext context = bitmap.CreateDrawingContext())
            {
                draw(context);
            }

            byte[] pixels = new byte[width * height * 4];
            fixed (byte* target = pixels)
            {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), (nint)target, pixels.Length, width * 4);
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
