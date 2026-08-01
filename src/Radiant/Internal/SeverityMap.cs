namespace Radiant.Internal
{
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Maps Radiant's compact 0..7 severity scale onto <see cref="LogLevel"/>. Kept in one place so
    /// the logs pipeline and any satellite bridge agree on the mapping.
    /// </summary>
    internal static class SeverityMap
    {
        internal static LogLevel ToLogLevel(int severity)
        {
            switch (RadiantMath.Clamp(severity, 0, 7))
            {
                case 0: return LogLevel.Trace;
                case 1: return LogLevel.Debug;
                case 2: return LogLevel.Information;
                case 3: return LogLevel.Information;
                case 4: return LogLevel.Warning;
                case 5: return LogLevel.Error;
                case 6: return LogLevel.Critical;
                default: return LogLevel.None;
            }
        }
    }
}
