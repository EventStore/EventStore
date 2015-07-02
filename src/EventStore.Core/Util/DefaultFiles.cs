namespace EventStore.Core.Util
{
    public class DefaultFiles
    {
        public static readonly string DefaultConfigFile;

        static DefaultFiles()
        {
            switch (Platforms.GetPlatform())
            {
                case Platform.Linux:
                    DefaultConfigFile = "eventstore.conf";
                    break;
                default:
                    DefaultConfigFile = System.String.Empty;
                    break;
            }
        }
    }
}
