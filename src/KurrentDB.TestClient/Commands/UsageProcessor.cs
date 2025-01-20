// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Linq;

namespace KurrentDB.TestClient.Commands;

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
