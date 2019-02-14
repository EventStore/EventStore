namespace EventStore.Core.Data {
	public static class ExpectedVersion {
		public const long Any = -2;

		public const long NoStream = -1;

		//public const int EmptyStream = 0;
		public const long Invalid = -3;
		public const long StreamExists = -4;
	}
}
