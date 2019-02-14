using EventStore.Common.Utils;

namespace EventStore.TestClient.Commands {
	internal class ExitProcessor : ICmdProcessor {
		public string Usage {
			get { return Keyword; }
		}

		public string Keyword {
			get { return "EXIT"; }
		}

		public bool Execute(CommandProcessorContext context, string[] args) {
			context.Log.Info("Exiting...");
			Application.Exit(ExitCode.Success, "Exit processor called.");
			return true;
		}
	}
}
