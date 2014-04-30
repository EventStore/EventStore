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

    /// <summary>
    /// Obsolete - Don't use this.  Both the -name value and /name:value styles are now both supported automatically.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    [Obsolete("The ArgStyle attribute is obsolete.  Both styles are now supported automatically")]
    public class ArgStyleAttribute : Attribute, IArgMetadata, ICommandLineArgumentsDefinitionMetadata
    {
        /// <summary>
        /// Obsolete - Don't use this.  Both the -name value and /name:value styles are now both supported automatically.
        /// </summary>
        public ArgStyle Style { get; set; }

        /// <summary>
        /// Obsolete - Don't use this.  Both the -name value and /name:value styles are now both supported automatically.
        /// </summary>
        /// <param name="style">obsolete</param>
        public ArgStyleAttribute(ArgStyle style = ArgStyle.PowerShell)
        {
            this.Style = style;
        }
    }
}
