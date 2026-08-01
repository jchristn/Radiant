namespace Radiant.Internal
{
    using System;
    using System.Diagnostics;
    using System.Reflection;

    /// <summary>
    /// Detects whether the hosting application was compiled as a Debug build, used to resolve
    /// <see cref="LabelPolicyEnum.Auto"/>. Radiant itself ships as a Release assembly, so a
    /// <c>#if DEBUG</c> here would reflect Radiant's build, not the consumer's. Inspecting the entry
    /// assembly's <see cref="DebuggableAttribute"/> reflects the application instead.
    /// </summary>
    internal static class BuildEnvironment
    {
        internal static bool IsDebugBuild()
        {
            Assembly? entry = Assembly.GetEntryAssembly();
            if (entry == null) return false;

            DebuggableAttribute? attribute = entry.GetCustomAttribute<DebuggableAttribute>();
            if (attribute == null) return false;

            return attribute.IsJITOptimizerDisabled;
        }

        internal static LabelPolicyEnum Resolve(LabelPolicyEnum policy)
        {
            if (policy != LabelPolicyEnum.Auto) return policy;
            return IsDebugBuild() ? LabelPolicyEnum.Strict : LabelPolicyEnum.Lenient;
        }
    }
}
