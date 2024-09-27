using System.Runtime;

namespace EventStore.Core.Util; 

public static class DefaultFiles {
    public static readonly string DefaultConfigFile = RuntimeInformation.IsWindows
        ? string.Empty
        : "eventstore.conf";
}
