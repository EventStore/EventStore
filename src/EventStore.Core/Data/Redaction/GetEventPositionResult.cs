// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Data.Redaction;

public enum GetEventPositionResult {
	None = 0,
	Success = 1,
	UnexpectedError = 2
}

public static class GetEventPositionResultExtensions {
	public static string GetErrorMessage(this GetEventPositionResult result) {
		return result switch {
			GetEventPositionResult.UnexpectedError => "An unexpected error has occurred.",
			_ => result.ToString()
		};
	}
}
