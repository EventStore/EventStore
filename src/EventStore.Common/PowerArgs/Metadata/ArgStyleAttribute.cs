using System;

namespace PowerArgs
{
    /// <summary>
    /// Obsolete, both the -name value and /name:value styles are supported automatically.
    /// </summary>
    [Obsolete("The ArgStyle attribute is obsolete.  Both styles are now supported automatically")]
    public enum ArgStyle
    {
        /// <summary>
        /// Obsolete, both the -name value and /name:value styles are supported automatically.
        /// </summary>
        PowerShell,
        /// <summary>
        /// Obsolete, both the -name value and /name:value styles are supported automatically.
        /// </summary>
        SlashColon
    }
}
