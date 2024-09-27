// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Data.Redaction {
	public enum ReleaseChunksLockResult {
		None = 0,
		Success = 1,
		Failed = 2
	}

	public static class ReleaseChunksLockResultExtensions {
		public static string GetErrorMessage(this ReleaseChunksLockResult result) {
			return result switch {
				ReleaseChunksLockResult.Failed => "Failed to release lock.",
				_ => result.ToString()
			};
		}
	}
}
