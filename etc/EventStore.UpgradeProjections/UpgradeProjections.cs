using System;
using System.Net;
using PowerArgs;

namespace EventStore.UpgradeProjections
{
    public class UpgradeProjectionOptions
    {
        [DefaultValue("127.0.0.1")]
        public string Ip { get; set; }
        [DefaultValue(1113)]
        public int Port { get; set; }
        [DefaultValue("admin")]
        public string UserName { get; set; }
        [DefaultValue("changeit")]
        public string Password { get; set; }
        [DefaultValue(true)]
        public bool Upgrade { get; set; }
        public bool Help { get; set; }
    }
    class UpgradeProjections
    {
        public static int Main(string[] args)
        {
            var options = Args.Parse<UpgradeProjectionOptions>(args);
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
