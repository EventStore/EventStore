namespace EventStore.ClientAPI.Transport.Http {
	internal static class ContentType {
		public const string Any = "*/*";

		public const string Xml = "text/xml";
		public const string PlainText = "text/plain";
		public const string Html = "text/html";

		public const string Atom = "application/atom+xml";
		public const string AtomJson = "application/atom+x.json";

		public const string AtomServiceDoc = "application/atomsvc+xml";
		public const string AtomServiceDocJson = "application/atomsvc+x.json";
	}
}
