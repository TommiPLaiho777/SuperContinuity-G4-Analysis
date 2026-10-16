using System;
using System.Globalization;

namespace Tommi.ContinuityTool
{
    /// <summary>High-precision curvature tolerance input; independent of document display precision.</summary>
    public static class G2Precision
    {
        public static string Format(double value)
        {
            // 17 significant decimal digits for double round-trip, also for sub-micron K2.
            return value.ToString("G17", CultureInfo.InvariantCulture);
        }
        public static bool TryNonnegative(string text, double maximum, out double value)
        {
            value=double.NaN;
            if (string.IsNullOrWhiteSpace(text)) return false;
            // Permit Finnish decimal commas and invariant decimal points, and exponent notation.
            string cleaned=text.Trim().Replace(',', '.');
            if (!double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return false;
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= maximum;
        }
    }
}
