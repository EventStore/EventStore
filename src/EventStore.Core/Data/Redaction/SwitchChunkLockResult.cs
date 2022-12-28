namespace EventStore.Core.Data.Redaction {
	public enum SwitchChunkLockResult {
		Success = 0,
		Failed = 1
	}

	public static class SwitchChunkLockResultExtensions {
		public static string GetErrorMessage(this SwitchChunkLockResult result) {
			return result switch {
				SwitchChunkLockResult.Failed => "Failed to acquire lock.",
				_ => result.ToString()
			};
		}
	}
}
