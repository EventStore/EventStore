using System;
using System.Threading;
using EventStore.ClientAPI.Embedded;
using EventStore.Common.Utils;
using EventStore.Core;

namespace EventStore.TestClient
{
    public class Program: ProgramBase<ClientOptions>
    {
        private ClusterVNode _node;
        private Client _client;

        public static void Main(string[] args)
        {
            Console.CancelKeyPress += delegate {
                Environment.Exit((int)ExitCode.Success);
            };
            var p = new Program();
            p.Run(args);
        }

        protected override string GetLogsDirectory(ClientOptions options)
        {
            return options.Log.IsNotEmptyString() ? options.Log : Helper.GetDefaultLogsDir();
        }

        protected override string GetComponentName(ClientOptions options)
        {
            return "client";
        }

        protected override void PreInit(ClientOptions options)
        {
            if (options.UseEmbeddedNode)
            {
                var runInMemory = string.IsNullOrWhiteSpace(options.EmbeddedNodeDataPath);
                var builder = (VNodeBuilder)EmbeddedVNodeBuilder.AsSingleNode();
                builder = runInMemory ? builder.RunInMemory() : builder.RunOnDisk(options.EmbeddedNodeDataPath);

                _node = builder.Build(options);
                _node.StartAndWaitUntilReady().GetAwaiter().GetResult();
            }
        }

        protected override void Create(ClientOptions options)
        {
            _client = new Client(options);
        }

        protected override void Start()
        {
            var exitCode = _client.Run();
            if (!_client.InteractiveMode)
            {
                Thread.Sleep(500);
                Application.Exit(exitCode, "Client non-interactive mode has exited.");
            }
        }

        public override void Stop()
        {
            if (_node != null)
            {
                _node.Stop();
            }
        }
    }
}