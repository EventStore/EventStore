using System.Collections.Generic;
using System.Linq;

namespace PowerArgs
{
    /// <summary>
    /// Any attribute that's purpose is to add information about a command line arguments definiton should
    /// derive from this type.  
    /// </summary>
    public interface IArgMetadata { }

    /// <summary>
    /// Represents IArgMetadata that is valid for CommandLineArguments
    /// </summary>
    public interface ICommandLineArgumentMetadata : IArgMetadata { }

    /// <summary>
    /// Represents IArgMetadata that is valid for CommandLineActions
    /// </summary>
    public interface ICommandLineActionMetadata : IArgMetadata { }

    /// <summary>
    /// Represents IArgMetadata that is valid for CommandLineArgumentsDefinition instances
    /// </summary>
    public interface ICommandLineArgumentsDefinitionMetadata : IArgMetadata { }

    /// <summary>
    /// Represents IArgMetadata that is valid for CommandLineArguments or CommandLineActions
    /// </summary>
    public interface IArgumentOrActionMetadata : ICommandLineActionMetadata, ICommandLineArgumentMetadata { }

    /// <summary>
    /// Represents IArgMetadata that is valid for CommandLineArguments, CommandLineActions, and CommandLineArgumentsDefinition instances
    /// </summary>
    public interface IGlobalArgMetadata : ICommandLineArgumentMetadata, ICommandLineActionMetadata, ICommandLineArgumentsDefinitionMetadata { }
}
