using System.Linq;

namespace EventStore.TestClient.Commands {
	internal class UsageProcessor : ICmdProcessor {
		public string Keyword {
			get { return "USAGE"; }
		}

		public string Usage {
			get { return "USAGE"; }
		}

		private readonly CommandsProcessor _commands;

		public UsageProcessor(CommandsProcessor commands) {
			_commands = commands;
		}

		public bool Execute(CommandProcessorContext context, string[] args) {
			var allCommands = _commands.RegisteredProcessors.Select(x => x.Usage.ToUpper());
			context.Log.Information("Available commands:");

			foreach (var command in allCommands) {
				context.Log.Information("    {command}", command);
			}

			return true;
		}
	}
}
