namespace EventStore.Common.Options
{
    public interface IOptions
    {
        bool ShowHelp { get; }
        bool ShowVersion { get; }
        string LogsDir { get; }
        string[] Defines { get; }
        bool Force { get; }
        bool Parse(params string[] args);
        string DumpOptions();
        string GetUsage();
    }
}