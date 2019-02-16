namespace EventStore.Projections.Core.Messages.Persisted.Commands {
	public class CreatePreparedCommand {
		public string Id { get; set; }
		public PersistedProjectionConfig Config { get; set; }
		public QuerySourcesDefinition SourceDefinition { get; set; }
		public PersistedProjectionVersion Version { get; set; }
		public string HandlerType { get; set; }
		public string Query { get; set; }
		public string Name { get; set; }
	}
}
