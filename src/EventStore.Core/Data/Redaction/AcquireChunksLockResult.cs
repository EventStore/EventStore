// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Data.Redaction {
	public enum AcquireChunksLockResult {
		None = 0,
		Success = 1,
		Failed = 2
	}

	public static class AcquireChunksLockResultExtensions {
		public static string GetErrorMessage(this AcquireChunksLockResult result) {
			return result switch {
				AcquireChunksLockResult.Failed => "Failed to acquire lock.",
				_ => result.ToString()
			};
		}
	}
}
