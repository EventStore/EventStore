namespace EventStore.Core.Authorization {
	public struct AssertionInformation {
		public Grant Grant { get; }
		private readonly string _assertion;

		public AssertionInformation(string type, string assertion, Grant grant) {
			Grant = grant;
			_assertion = $"{type}:{assertion}:{grant}";
		}

		public override string ToString() {
			return _assertion;
		}
	}
}
