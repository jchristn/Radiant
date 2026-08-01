namespace Radiant
{
    using System;
    using Microsoft.Extensions.Logging;
    using OpenTelemetry.Resources;
    using Radiant.Internal;

    /// <summary>
    /// Wires Radiant's OTLP / Loki log export into an <see cref="ILoggingBuilder"/> an application
    /// already owns — for example the builder configured by a .NET generic host, or the one an app
    /// also passes to a third-party sink such as SyslogLogging's <c>AddSyslog</c>. Use this when the
    /// application controls its own logging pipeline; when it does not, let
    /// <see cref="RadiantHost"/> build the logs pipeline and call
    /// <see cref="RadiantHost.CreateLogger(string)"/>.
    /// </summary>
    public static class RadiantLoggingExtensions
    {
        /// <summary>
        /// Add Radiant's OpenTelemetry log exporter (OTLP collector and/or direct Loki, per
        /// <paramref name="settings"/>) to the logging builder, and set the builder's minimum level
        /// from <see cref="LogsSettings.MinimumSeverity"/>.
        /// </summary>
        /// <param name="builder">The logging builder. Must be non-null.</param>
        /// <param name="settings">
        /// The Radiant settings whose <see cref="RadiantSettings.Logs"/>, <see cref="RadiantSettings.Otlp"/>,
        /// and <see cref="RadiantSettings.Loki"/> sections drive the exporter. Must be non-null with a
        /// non-empty <see cref="RadiantSettings.ServiceName"/>.
        /// </param>
        /// <returns>The same builder, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <see cref="RadiantSettings.ServiceName"/> is null or empty.</exception>
        public static ILoggingBuilder AddRadiant(this ILoggingBuilder builder, RadiantSettings settings)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (String.IsNullOrWhiteSpace(settings.ServiceName))
                throw new ArgumentException("RadiantSettings.ServiceName is required and must be non-empty.", nameof(settings));

            if (!settings.Enable || !settings.Logs.Enable) return builder;

            string instanceId = !String.IsNullOrWhiteSpace(settings.ServiceInstanceId)
                ? settings.ServiceInstanceId!
                : Guid.NewGuid().ToString();

            ResourceBuilder resource = PipelineFactory.BuildResource(settings, instanceId);

            builder.SetMinimumLevel(PipelineFactory.ComputeLogMinimum(settings));
            builder.AddOpenTelemetry(options => PipelineFactory.ConfigureLogging(options, settings, resource));
            return builder;
        }
    }
}
