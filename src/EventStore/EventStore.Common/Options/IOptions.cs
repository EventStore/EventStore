namespace EventStore.Common.Options
{
    public interface IOptions
    {
        bool ShowHelp { get; }
        bool ShowVersion { get; }
        string Config { get; }
        string LogsDir { get; }
        string[] Defines { get; }
        bool Force { get; }
    }
}