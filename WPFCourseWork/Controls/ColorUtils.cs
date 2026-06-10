using System.Windows.Media;

namespace WPFCourseWork.Controls;

public static class ColorUtils
{
    public static (double H, double S, double V) RgbToHsv(byte r, byte g, byte b)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;

        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double delta = max - min;

        double h = 0;
        if (delta > 0.00001)
        {
            if (Math.Abs(max - rd) < 0.00001)
            {
                h = 60 * (((gd - bd) / delta) % 6);
            }
            else if (Math.Abs(max - gd) < 0.00001)
            {
                h = 60 * (((bd - rd) / delta) + 2);
            }
            else
            {
                h = 60 * (((rd - gd) / delta) + 4);
            }
        }

        if (h < 0)
        {
            h += 360;
        }

        double s = max <= 0.00001 ? 0 : delta / max;
        double v = max;
        return (h, s, v);
    }

    public static Color HsvToRgb(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Clamp(s, 0, 1);
        v = Math.Clamp(v, 0, 1);

        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60.0 % 2) - 1));
        double m = v - c;

        double r1, g1, b1;
        if (h < 60)
        {
            (r1, g1, b1) = (c, x, 0);
        }
        else if (h < 120)
        {
            (r1, g1, b1) = (x, c, 0);
        }
        else if (h < 180)
        {
            (r1, g1, b1) = (0, c, x);
        }
        else if (h < 240)
        {
            (r1, g1, b1) = (0, x, c);
        }
        else if (h < 300)
        {
            (r1, g1, b1) = (x, 0, c);
        }
        else
        {
            (r1, g1, b1) = (c, 0, x);
        }

        return Color.FromRgb(
            (byte)Math.Round((r1 + m) * 255),
            (byte)Math.Round((g1 + m) * 255),
            (byte)Math.Round((b1 + m) * 255));
    }

    public static string ColorToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static bool TryParseHexColor(string? hex, out Color color)
    {
        color = Colors.Black;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        string value = hex.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length != 6)
        {
            return false;
        }

        if (!byte.TryParse(value[..2], System.Globalization.NumberStyles.HexNumber, null, out byte r) ||
            !byte.TryParse(value[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g) ||
            !byte.TryParse(value[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte b))
        {
            return false;
        }

        color = Color.FromRgb(r, g, b);
        return true;
    }
}
