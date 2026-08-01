namespace Radiant.Internal
{
    /// <summary>
    /// Clamp helpers usable on every target framework. <c>System.Math.Clamp</c> is not available
    /// on <c>netstandard2.0</c>, so setters throughout the settings tree route through here rather
    /// than calling <c>Math.Clamp</c> directly.
    /// </summary>
    internal static class RadiantMath
    {
        internal static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        internal static long Clamp(long value, long min, long max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        internal static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
