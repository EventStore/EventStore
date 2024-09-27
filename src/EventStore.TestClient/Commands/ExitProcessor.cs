// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using EventStore.Common.Utils;

namespace EventStore.TestClient.Commands {
	internal class ExitProcessor : ICmdProcessor {
		private readonly CancellationTokenSource _cancellationTokenSource;

		public ExitProcessor(CancellationTokenSource cancellationTokenSource) {
			_cancellationTokenSource = cancellationTokenSource;
		}

		public string Usage {
			get { return Keyword; }
		}

		public string Keyword {
			get { return "EXIT"; }
		}

		public bool Execute(CommandProcessorContext context, string[] args) {
			context.Log.Information("Exiting...");
			_cancellationTokenSource.Cancel();
			return true;
		}
	}
}
