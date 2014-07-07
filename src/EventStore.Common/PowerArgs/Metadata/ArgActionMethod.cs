using System;

namespace PowerArgs
{
    /// <summary>
    /// Use this attribute to annotate methods that represent your program's actions.  
    /// </summary>
    public class ArgActionMethod : Attribute, ICommandLineActionMetadata { }
}
