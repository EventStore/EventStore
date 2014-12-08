using System;
using System.Net;
using EventStore.Rags;
using EventStore.Common.Options;

namespace EventStore.UpgradeProjections
{
    public class UpgradeProjectionOptions
    {
        public string Ip { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public bool Upgrade { get; set; }
        public bool Help { get; set; }
        public UpgradeProjectionOptions()
        {
            Ip = "127.0.0.1";
            Port = 1113;
            UserName = "admin";
            Password = "changeit";
            Upgrade = true;
        }
    }
    class UpgradeProjections
    {
        public static int Main(string[] args)
        {
            var options = CommandLine.Parse<UpgradeProjectionOptions>(args)
                            .Cleanup()
                            .ApplyTo<UpgradeProjectionOptions>();
            if (options.Help)
            {
                Console.Write(ArgUsage.GetUsage<UpgradeProjectionOptions>());
                return 0;
            }
            try
            {
                var upgrade = new UpgradeProjectionsWorker(
                    IPAddress.Parse(options.Ip), options.Port, options.UserName, options.Password, options.Upgrade);
                upgrade.RunUpgrade();
                return 0;
            }
            catch (AggregateException ex)
            {
                Console.Error.WriteLine(ex.Message);
                foreach (var inner in ex.InnerExceptions)
                {
                    Console.Error.WriteLine(inner.Message);
                }
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }
}
