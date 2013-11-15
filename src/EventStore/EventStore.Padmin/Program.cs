using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using EventStore.ClientAPI;
using System.Linq;

namespace EventStore.Padmin
{
    public class Program
    {
        private static readonly Dictionary<string, Action<ProjectionsManager, string[]>> Commands =
            new Dictionary<string, Action<ProjectionsManager, string[]>>();
  
        public static int Main(string[] args)
        {
            if(args.Length == 0)
            {
                ShowUsage();
                return 0;
            }

            RegisterCommands();
            var config = LoadConfig();
            return TryExecute(config, args) ? 0 : 1;
        }

        private static void ShowUsage()
        {
            Log(Environment.NewLine);
            Log("padmin <command> <arg1 arg2 ... argN>");
            Log("commands available : ");
            Log(Environment.NewLine);

            Log("enable <projection-name> [<login> <password>]");
            Log("disable <projection-name> [<login> <password>]");

            Log("create <onetime | adhoc | continuous | persistent> <projection-name> <query file> [<login> <password>]");
            Log("list <all | onetime | adhoc | continuous | persistent> [<login> <password>]");

            Log("status <projection-name> [<login> <password>]");
            Log("state <projection-name> [<login> <password>]");
            Log("statistics <projection-name> [<login> <password>]");

            Log("show-query <projection-name> [<login> <password>]");
            Log("update-query <projection-name> <query file> [<login> <password>]");

            Log("delete <projection-name> [<login> <password>]");
        }

        private static void RegisterCommands()
        {
            Commands.Add("enable", Command.Enable);
            Commands.Add("disable", Command.Disable);

            Commands.Add("create", Command.Create);
            Commands.Add("list", Command.List);

            Commands.Add("status", Command.Status);
            Commands.Add("state", Command.State);
            Commands.Add("statistics", Command.Statistics);

            Commands.Add("show-query", Command.ShowQuery);
            Commands.Add("update-query", Command.UpdateQuery);

            Commands.Add("delete", Command.Delete);
        }

        private static bool TryExecute(Dictionary<string, string> config, string[] args)
        {
            try
            {
                Log("Loading config values...");

                var ip = IPAddress.Parse(config["ip"]);
                var port = int.Parse(config["http-port"]);
                var endPoint = new IPEndPoint(ip, port);

                var manager = new ProjectionsManager(new ConsoleLogger(), endPoint);
                Execute(manager, args);
                return true;
            }
            catch (Exception e)
            {
                Log("padmin execution failed : {0}", e);
                return false;
            }
        }

        private static void Execute(ProjectionsManager manager, string[] args)
        {
            var command = args[0].Trim().ToLower();
            var commandArgs = args.Skip(1).ToArray();

            Action<ProjectionsManager, string[]> executor;

            if(Commands.TryGetValue(command, out executor))
                executor(manager, commandArgs);
            else
                Log("{0} is not a recognized command", args[0]);
        }

        private static Dictionary<string, string> LoadConfig()
        {
            try
            {
                var config = new Dictionary<string, string>();

                var text = File.ReadAllText("padmin.esconfig");
                var lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var kvp = line.Split(new[] {"="}, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().ToLower()).ToArray();
                    if (kvp.Length == 2 && !config.ContainsKey(kvp[0]))
                        config[kvp[0]] = kvp[1];
                }

                FillDefaults(config);
                return config;
            }
            catch (Exception e)
            {
                Log("Config file failed to load ({0}).\n" +
                    "Should be located near exe file, named 'padmin.esconfig', key=value formatted). Using defaults",
                    e.Message);

                var config = new Dictionary<string, string>();
                FillDefaults(config);
                return config;
            }
        }

        private static void FillDefaults(Dictionary<string, string> config)
        {
            if (!config.ContainsKey("ip"))
                config["ip"] = "127.0.0.1";
            if (!config.ContainsKey("http-port"))
                config["http-port"] = "2113";
        }

        private static void Log(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }
    }
}
