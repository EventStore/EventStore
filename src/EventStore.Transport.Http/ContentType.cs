namespace EventStore.Transport.Http {
	public static class ContentType {
		public const string Any = "*/*";

		public const string Json = "application/json";
		public const string Xml = "text/xml";
		public const string ApplicationXml = "application/xml";
		public const string PlainText = "text/plain";
		public const string Html = "text/html";

		public const string Atom = "application/atom+xml";
		public const string AtomJson = "application/vnd.eventstore.atom+json";

		public const string AtomServiceDoc = "application/atomsvc+xml";
		public const string AtomServiceDocJson = "application/vnd.eventstore.atomsvc+json";

		public const string EventJson = "application/vnd.eventstore.event+json";
		public const string EventXml = "application/vnd.eventstore.event+xml";

		public const string EventsJson = "application/vnd.eventstore.events+json";
		public const string EventsXml = "application/vnd.eventstore.events+xml";

		public const string DescriptionDocJson = "application/vnd.eventstore.streamdesc+json";

		public const string Competing = "application/vnd.eventstore.competingatom+xml";
		public const string CompetingJson = "application/vnd.eventstore.competingatom+json";

		public const string Raw = "application/octet-stream";
	}
}
