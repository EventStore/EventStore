namespace EventStore.Core.Data.Redaction {
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
}
