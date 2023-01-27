namespace EventStore.Core.Data.Redaction {
	public enum GetEventPositionResult {
		Success = 0,
		UnexpectedError = 1
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
