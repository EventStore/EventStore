using EventStore.Common.Options;

namespace EventStore.Core.Tests.Common
{
    public class TestArgs : IOptions
    {
        public bool ShowHelp { get; set; }
        public bool ShowVersion { get; set; }
        public string Config { get; set; }
        public string Logsdir { get; set; }
        public string[] Defines { get; set; }
        public bool Force { get; set; }
        public RunProjections RunProjections { get; set; }

        public int HttpPort { get; set; }
        public TestArgs()
        {
            HttpPort = 2111;
            Logsdir = "~/logs";
        }
    }
}
