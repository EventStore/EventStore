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
			var allCommands = string.Join("\n\n", _commands.RegisteredProcessors.Select(x => x.Usage.ToUpper()));
			context.Log.Info("Available commands:\n{allCommands}", allCommands);
			return true;
		}
	}
}
