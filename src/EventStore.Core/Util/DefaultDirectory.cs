using System.IO;
using System.Reflection;

namespace EventStore.Core.Util
{
    public class DefaultDirectory
    {
        public static readonly string DefaultContentDirectory;
        public static readonly string DefaultConfigurationDirectory;
        public static readonly string DefaultDataDirectory;
        public static readonly string DefaultLogDirectory;
        public static readonly string DefaultTestClientLogDirectory;

        static DefaultDirectory()
        {
            switch (Platforms.GetPlatform())
            {
                case Platform.Linux:
                    DefaultContentDirectory = "/usr/share/eventstore";
                    DefaultConfigurationDirectory = "/etc/eventstore";
                    DefaultDataDirectory = "/var/lib/eventstore";
                    DefaultLogDirectory = "/var/log/eventstore";
                    DefaultTestClientLogDirectory = Path.Combine(GetExecutingDirectory(), "testclientlog");
                    break;
                default:
                    DefaultContentDirectory = GetExecutingDirectory();
                    DefaultConfigurationDirectory = GetExecutingDirectory();
                    DefaultDataDirectory = Path.Combine(GetExecutingDirectory(), "data");
                    DefaultLogDirectory = Path.Combine(GetExecutingDirectory(), "logs");
                    DefaultTestClientLogDirectory = Path.Combine(GetExecutingDirectory(), "testclientlog");
                    break;
            }
        }

        private static string GetExecutingDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }
    }
}
