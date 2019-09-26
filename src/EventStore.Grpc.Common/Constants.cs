namespace EventStore.Grpc {
	internal static class Constants {
		public static class Exceptions {
			public const string ExceptionKey = "exception";

			public const string AccessDenied = "access-denied";
			public const string InvalidTransaction = "invalid-transaction";
			public const string StreamDeleted = "stream-deleted";
			public const string WrongExpectedVersion = "wrong-expected-version";
			public const string NotFound = "not-found";

			public const string ExpectedVersion = "expected-version";
			public const string ActualVersion = "actual-version";
			public const string StreamName = "stream-name";
		}

		public static class Metadata {
			public const string IsJson = "is-json";
			public const string Type = "type";
			public const string Created = "created";
		}

		public static class Headers {
			public const string Authorization = "authorization";
			public const string BasicScheme = "Basic";
		}
	}
}
