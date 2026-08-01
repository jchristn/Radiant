namespace Radiant
{
    /// <summary>
    /// How the optional declared catalog reacts when a measurement carries a label (tag) key that
    /// was not declared for the instrument. Applies only when the catalog is used
    /// (<see cref="MetricsSettings.Definitions"/> is populated); the plain BCL emit path is never
    /// subject to this policy.
    /// </summary>
    public enum LabelPolicyEnum
    {
        /// <summary>
        /// Resolve to <see cref="Strict"/> in a Debug build and <see cref="Lenient"/> in a Release
        /// build. The default: undeclared labels surface loudly during development and degrade
        /// gracefully in production.
        /// </summary>
        Auto,

        /// <summary>
        /// Throw an <see cref="System.ArgumentException"/> when a measurement carries an undeclared
        /// label key.
        /// </summary>
        Strict,

        /// <summary>
        /// Drop the undeclared label key, emit a one-time diagnostic warning, and record the
        /// measurement with its remaining declared labels.
        /// </summary>
        Lenient
    }
}
