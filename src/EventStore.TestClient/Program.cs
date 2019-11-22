using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core;

namespace EventStore.TestClient {
	public class Program : ProgramBase<ClientOptions> {
		private Client _client;

		public Program(string[] args) : base(args) {
			
		}

		public static Task<int> Main(string[] args) {
			Console.CancelKeyPress += delegate { Environment.Exit((int)ExitCode.Success); };
			var p = new Program(args);
			return p.Run();
		}

		protected override string GetLogsDirectory(ClientOptions options) {
			return options.Log.IsNotEmptyString() ? options.Log : Helper.GetDefaultLogsDir();
		}

		protected override string GetComponentName(ClientOptions options) {
			return "client";
		}

		protected override bool GetIsStructuredLog(ClientOptions options) {
			return false;
		}

		protected override void Create(ClientOptions options) {
			_client = new Client(options);
		}

		protected override Task Start() {
			var exitCode = _client.Run();
			if (!_client.InteractiveMode) {
				Thread.Sleep(500);
				Application.Exit(exitCode, "Client non-interactive mode has exited.");
			}
			return Task.CompletedTask;
		}

		public override Task Stop() => Task.CompletedTask;
	}
}
