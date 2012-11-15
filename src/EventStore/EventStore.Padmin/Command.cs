using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EventStore.ClientAPI;

namespace EventStore.Padmin
{
    internal static class Command
    {
        public static void Enable(ProjectionsManager manager, string[] commandArgs)
        {
            var names = GetProjectionNames(commandArgs);
            foreach (var name in names)
            {
                Log("Enabling {0}...", name);
                manager.Enable(name);
                Log("{0} enabled", name);
            }
        }

        public static void Disable(ProjectionsManager manager, string[] commandArgs)
        {
            var names = GetProjectionNames(commandArgs);
            foreach (var name in names)
            {
                Log("Disabling {0}...", name);
                manager.Disable(name);
                Log("{0} disabled", name);
            }
        }

        public static void Create(ProjectionsManager manager, string[] commandArgs)
        {
            if (commandArgs.Length < 2)
            {
                Log("Invalid argument value (projection type)");
                return;
            }

            var type = commandArgs[0].Trim().ToLower();
            string pname;
            var query = GetQuery(commandArgs.Skip(1).ToArray(), out pname);

            if(query == null || (type != "onetime" && string.IsNullOrEmpty(pname)))
            {
                Log("Invalid arguments");
                return;
            }

            switch (type)
            {
                case "onetime":
                    Log("Creating onetime projection...");
                    manager.CreateOneTime(query);
                    Log("Created");
                    break;
                case "adhoc":
                    Log("Creating adhoc projection {0}...", pname);
                    manager.CreateAdHoc(pname, query);
                    Log("Created");
                    break;
                case "continuous":
                    Log("Creating continuous projection {0}...", pname);
                    manager.CreateContinuous(pname, query);
                    Log("Created");
                    break;
                case "persistent":
                    Log("Creating persistent projection {0}...", pname);
                    manager.CreatePersistent(pname, query);
                    Log("Created");
                    break;
                default:
                    Log("projection type not recognized");
                    break;
            }
        }

        public static void List(ProjectionsManager manager, string[] commandArgs)
        {
            if (commandArgs.Length != 1)
            {
                Log("Invalid argument value for list mode");
                return;
            }

            var mode = commandArgs[0].Trim().ToLower();
            switch (mode)
            {
                case "all":
                    Log("Listing all projections...");
                    LogUnformatted(manager.ListAll());
                    Log("All projections listed");
                    break;
                case "onetime":
                    Log("Listing onetime projections...");
                    LogUnformatted(manager.ListOneTime());
                    Log("Onetime projections listed");
                    break;
                case "adhoc":
                    Log("Listing adhoc projections...");
                    LogUnformatted(manager.ListAdHoc());
                    Log("Adhoc projections listed");
                    break;
                case "continuous":
                    Log("Listing continuous projections...");
                    LogUnformatted(manager.ListContinuous());
                    Log("Continuous projections listed");
                    break;
                case "persistent":
                    Log("Listing persistent projections...");
                    LogUnformatted(manager.ListPersistent());
                    Log("Persistent projections listed");
                    break;
                default:
                    Log("List mode not recognized");
                    break;
            }
        }

        public static void Status(ProjectionsManager manager, string[] commandArgs)
        {
            var names = GetProjectionNames(commandArgs);
            foreach (var name in names)
            {
                Log("{0} is '{1}'", name, manager.GetStatus(name));
            }
        }

        public static void State(ProjectionsManager manager, string[] commandArgs)
        {
            var names = GetProjectionNames(commandArgs);
            foreach (var name in names)
            {
                Log("{0}'s state is : '{1}'", name, manager.GetState(name));
            }
        }

        public static void Statistics(ProjectionsManager manager, string[] commandArgs)
        {
            var names = GetProjectionNames(commandArgs);
            foreach (var name in names)
            {
                Log("{0}'s statistics :", name);
                LogUnformatted(manager.GetStatistics(name));
            }
        }

        public static void ShowQuery(ProjectionsManager manager, string[] commandArgs)
        {
            var names = GetProjectionNames(commandArgs);
            foreach (var name in names)
            {
                Log("{0}'s query :", name);
                LogUnformatted(manager.GetQuery(name));
            }
        }

        public static void UpdateQuery(ProjectionsManager manager, string[] commandArgs)
        {
            string pname;
            var query = GetQuery(commandArgs, out pname);

            if (query != null)
            {
                Log("Updating query of {0}...", pname);
                manager.UpdateQuery(pname, query);
                Log("Query updated");
            }
            else
                Log("Invalid arguments for command update query");
        }

        public static void Delete(ProjectionsManager manager, string[] commandArgs)
        {
            var names = GetProjectionNames(commandArgs);
            foreach (var name in names)
            {
                Log("Deleting {0}...", name);
                manager.Delete(name);
                Log("{0} deleted", name);
            }
        }

        private static IEnumerable<string> GetProjectionNames(string[] commandArgs)
        {
            if (commandArgs.Length != 1)
            {
                Log("Invalid argument value (projection-name)");
                return new string[0];
            }
            return commandArgs;
        }

        private static string GetQuery(string[] commandArgs, out string name)
        {
            if (commandArgs.Length != 1 && commandArgs.Length != 2)
            {
                name = null;
                return null;
            }

            name = null;
            if (commandArgs.Length == 1)
            {
                return File.ReadAllText(commandArgs[0]);
            }

            if (commandArgs.Length == 2)
            {
                name = commandArgs[0];
                return File.ReadAllText(commandArgs[1]);
            }

            return null;
        }

        private static void Log(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        private static void LogUnformatted(string s)
        {
            Console.WriteLine(s);
        }
    }
}