namespace PowerArgs
{
    /// <summary>
    /// Use this attribute to hide an argument from the usage output.  Users will still be able to provide
    /// the argument, but it will be undocumented.  This is useful if you want to invlude some secret commands
    /// or diagnostic commands.
    /// </summary>
    public class ArgHiddenFromUsage : UsageHook
    {
        /// <summary>
        /// Sets the ignore flag on the info context so the usage generator skips this argument.
        /// </summary>
        /// <param name="info">The info about the argument we're hiding</param>
        public override void BeforeGenerateUsage(ArgumentUsageInfo info)
        {
            info.Ignore = true;
        }
    }
}
