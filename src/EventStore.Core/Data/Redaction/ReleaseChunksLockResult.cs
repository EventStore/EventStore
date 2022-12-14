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
