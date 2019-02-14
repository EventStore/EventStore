namespace EventStore.Common.Utils {
	public static class StringExtensions {
		public static bool IsEmptyString(this string s) {
			return string.IsNullOrEmpty(s);
		}

		public static bool IsNotEmptyString(this string s) {
			return !string.IsNullOrEmpty(s);
		}
	}
}
