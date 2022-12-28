namespace EventStore.Core.Data.Redaction {
	public enum SwitchChunkUnlockResult {
		Success = 0,
		Failed = 1
	}

	public static class SwitchChunkUnlockResultExtensions {
		public static string GetErrorMessage(this SwitchChunkUnlockResult result) {
			return result switch {
				SwitchChunkUnlockResult.Failed => "Failed to release lock.",
				_ => result.ToString()
			};
		}
	}
}
