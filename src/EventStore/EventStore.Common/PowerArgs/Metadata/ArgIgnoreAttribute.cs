using System;

namespace PowerArgs
{
    /// <summary>
    /// Use this attribute if you want PowerArgs to ignore a property completely.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ArgIgnoreAttribute : Attribute, IArgumentOrActionMetadata { }
}
