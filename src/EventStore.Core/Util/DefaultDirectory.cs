using System;
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
            switch (GetPlatform())
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

        private enum Platform
        {
            Windows,
            Linux,
            Mac
        }

        private static Platform GetPlatform()
        {
            //http://stackoverflow.com/questions/10138040/how-to-detect-properly-windows-linux-mac-operating-systems
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    if (Directory.Exists("/Applications") & Directory.Exists("/System") & Directory.Exists("/Users") & Directory.Exists("/Volumes"))
                        return Platform.Mac;
                    return Platform.Linux;

                case PlatformID.MacOSX:
                    return Platform.Mac;

                default:
                    return Platform.Windows;
            }
        }
    }
}
