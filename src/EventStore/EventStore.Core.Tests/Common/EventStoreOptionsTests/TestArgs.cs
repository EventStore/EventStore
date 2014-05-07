using EventStore.Common.Options;

namespace EventStore.Core.Tests.Common
{
    public class TestArgs : IOptions
    {
        public bool Help { get; set; }
        public bool Version { get; set; }
        public string Config { get; set; }
        public string Log { get; set; }
        public string[] Defines { get; set; }
        public bool Force { get; set; }
        public ProjectionType RunProjections { get; set; }

        public int HttpPort { get; set; }
        public TestArgs()
        {
            HttpPort = 2111;
            Log = "~/logs";
        }
    }
}
