namespace EventStore.Core.Data.Redaction {
	public enum ReadEventInfoResult {
		Success = 0,
		UnexpectedError = 1
	}

	public static class ReadEventInfoResultExtensions {
		public static string GetErrorMessage(this ReadEventInfoResult result) {
			return result switch {
				ReadEventInfoResult.UnexpectedError => "An unexpected error has occurred.",
				_ => result.ToString()
			};
		}
	}
}
