using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;

namespace EventStore.Padmin
{
    internal static class Command
    {
        public static void Enable(ProjectionsManager manager, string[] commandArgs)
        {
            var nameAndCredentials = GetProjectionNameAndCredentials(commandArgs);
            var name = nameAndCredentials.Item1;
            Log("Enabling {0}...", name);
            manager.Enable(name, nameAndCredentials.Item2);
            Log("{0} enabled", name);
        }

        public static void Disable(ProjectionsManager manager, string[] commandArgs)
        {
            var nameAndCredentials = GetProjectionNameAndCredentials(commandArgs);
            var name = nameAndCredentials.Item1;
            Log("Disabling {0}...", nameAndCredentials.Item1);
            manager.Disable(name, nameAndCredentials.Item2);
            Log("{0} disabled", name);
        }

        public static void Create(ProjectionsManager manager, string[] commandArgs)
        {
            if (commandArgs.Length < 2)
            {
                Log("Invalid argument value (projection type)");
                return;
            }

            var type = commandArgs[0].Trim().ToLower();
            var queryInfo = GetQueryAndCredentials(commandArgs.Skip(1).ToArray());
            if (queryInfo == null || (type != "onetime" && string.IsNullOrEmpty(queryInfo.Item1)))
            {
                Log("Invalid arguments");
                return;
            }
            var pname = queryInfo.Item1;
            var query = queryInfo.Item2;
            var userCredentials = queryInfo.Item3;

            switch (type)
            {
                case "onetime":
                    Log("Creating onetime projection...");
                    manager.CreateOneTime(query, userCredentials);
                    Log("Created");
                    break;
                case "continuous":
                    Log("Creating continuous projection {0}...", pname);
                    manager.CreateContinuous(pname, query, userCredentials);
                    Log("Created");
                    break;
                default:
                    Log("projection type not recognized");
                    break;
            }
        }

        public static void List(ProjectionsManager manager, string[] commandArgs)
        {
            if (commandArgs.Length != 1 && commandArgs.Length != 3)
            {
                Log("Invalid argument value for list mode");
                return;
            }
            var userCredentials = commandArgs.Length == 3 ? new UserCredentials(commandArgs[1], commandArgs[2]) : null;
            var mode = commandArgs[0].Trim().ToLower();
            switch (mode)
            {
                case "all":
                    Log("Listing all projections...");
                    LogUnformatted(manager.ListAll(userCredentials));
                    Log("All projections listed");
                    break;
                case "onetime":
                    Log("Listing onetime projections...");
                    LogUnformatted(manager.ListOneTime(userCredentials));
                    Log("Onetime projections listed");
                    break;
                case "continuous":
                    Log("Listing continuous projections...");
                    LogUnformatted(manager.ListContinuous(userCredentials));
                    Log("Continuous projections listed");
                    break;
                default:
                    Log("List mode not recognized");
                    break;
            }
        }

        public static void Status(ProjectionsManager manager, string[] commandArgs)
        {
            var nameAndCredentials = GetProjectionNameAndCredentials(commandArgs);
            if (nameAndCredentials == null)
            {
                Log("Invalid arguments, should be: <projection-name> [<login> <password>].");
                return;
            }

            var name = nameAndCredentials.Item1;
            Log("{0} is '{1}'", name, manager.GetStatus(name, nameAndCredentials.Item2));
        }

        public static void State(ProjectionsManager manager, string[] commandArgs)
        {
            var nameAndCredentials = GetProjectionNameAndCredentials(commandArgs);
            if (nameAndCredentials == null)
            {
                Log("Invalid arguments, should be: <projection-name> [<login> <password>].");
                return;
            }
            var name = nameAndCredentials.Item1;
            Log("{0}'s state is : '{1}'", name, manager.GetState(name, nameAndCredentials.Item2));
        }

        public static void Statistics(ProjectionsManager manager, string[] commandArgs)
        {
            var nameAndCredentials = GetProjectionNameAndCredentials(commandArgs);
            if (nameAndCredentials == null)
            {
                Log("Invalid arguments, should be: <projection-name> [<login> <password>].");
                return;
            }
            var name = nameAndCredentials.Item1;
            Log("{0}'s statistics :", name);
            LogUnformatted(manager.GetStatistics(name, nameAndCredentials.Item2));
        }

        public static void ShowQuery(ProjectionsManager manager, string[] commandArgs)
        {
            var nameAndCredentials = GetProjectionNameAndCredentials(commandArgs);
            if (nameAndCredentials == null)
            {
                Log("Invalid arguments, should be: <projection-name> [<login> <password>].");
                return;
            }
            var name = nameAndCredentials.Item1;
            Log("{0}'s query :", name);
            LogUnformatted(manager.GetQuery(name, nameAndCredentials.Item2));
        }

        public static void UpdateQuery(ProjectionsManager manager, string[] commandArgs)
        {
            var queryInfo = GetQueryAndCredentials(commandArgs);
            if (queryInfo == null)
            {
                Log("Invalid arguments for command update query");
                return;
            }
            var pname = queryInfo.Item1;
            var query = queryInfo.Item2;
            var userCredentials = queryInfo.Item3;
            Log("Updating query of {0}...", pname);
            manager.UpdateQuery(pname, query, userCredentials);
            Log("Query updated");
        }

        public static void Delete(ProjectionsManager manager, string[] commandArgs)
        {
            var nameAndCredentials = GetProjectionNameAndCredentials(commandArgs);
            if (nameAndCredentials == null)
            {
                Log("Invalid arguments, should be: <projection-name> [<login> <password>].");
                return;
            }
            var name = nameAndCredentials.Item1;
            Log("Deleting {0}...", name);
            manager.Delete(name, nameAndCredentials.Item2);
            Log("{0} deleted", name);
        }

        private static Tuple<string, UserCredentials> GetProjectionNameAndCredentials(string[] commandArgs)
        {
            if (commandArgs.Length != 1 && commandArgs.Length != 3)
                return null;
            return Tuple.Create(commandArgs[0],
                                commandArgs.Length == 3 ? new UserCredentials(commandArgs[1], commandArgs[2]) : null);
        }

        private static Tuple<string, string, UserCredentials> GetQueryAndCredentials(string[] commandArgs)
        {
            if (commandArgs.Length == 3 || commandArgs.Length >= 4)
                return null;

            if (commandArgs.Length == 1 || commandArgs.Length == 3)
            {
                return Tuple.Create(
                    (string)null,
                    File.ReadAllText(commandArgs[0]),
                    commandArgs.Length == 3 ? new UserCredentials(commandArgs[1], commandArgs[2]) : null);
            }

            if (commandArgs.Length == 2 || commandArgs.Length == 4)
            {
                return Tuple.Create(
                    commandArgs[0],
                    File.ReadAllText(commandArgs[1]),
                    commandArgs.Length == 4 ? new UserCredentials(commandArgs[2], commandArgs[3]) : null);
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