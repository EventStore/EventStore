using System;

namespace EventStore.ClientAPI.Common.Utils {
	static class Ensure {
		public static void NotNull<T>(T argument, string argumentName) where T : class {
			if (argument == null)
				throw new ArgumentNullException(argumentName);
		}

		public static void NotNullOrEmpty(string argument, string argumentName) {
			if (string.IsNullOrEmpty(argument))
				throw new ArgumentNullException(argument, argumentName);
		}

		public static void GreaterThanOrEqualTo(long number, long minimum, string argumentName) {
			if (number < minimum)
				throw new ArgumentOutOfRangeException(argumentName,
					$"{argumentName} should be greater than or equal to {minimum}.");
		}

		public static void Positive(int number, string argumentName) {
			if (number <= 0)
				throw new ArgumentOutOfRangeException(argumentName, argumentName + " should be positive.");
		}

		public static void Positive(long number, string argumentName) {
			if (number <= 0)
				throw new ArgumentOutOfRangeException(argumentName, argumentName + " should be positive.");
		}

		public static void Nonnegative(long number, string argumentName) {
			if (number < 0)
				throw new ArgumentOutOfRangeException(argumentName, argumentName + " should be non-negative.");
		}

		public static void Nonnegative(int number, string argumentName) {
			if (number < 0)
				throw new ArgumentOutOfRangeException(argumentName, argumentName + " should be non-negative.");
		}

		public static void NotEmptyGuid(Guid guid, string argumentName) {
			if (Guid.Empty == guid)
				throw new ArgumentException(argumentName, argumentName + " should be non-empty GUID.");
		}

		public static void Equal(int expected, int actual) {
			if (expected != actual)
				throw new Exception(string.Format("expected {0} actual {1}", expected, actual));
		}
	}
}
