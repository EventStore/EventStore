using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Standard {
	public class StubHandler : IProjectionStateHandler {
		//private readonly char _separator;
		//private readonly string _categoryStreamPrefix;

		public StubHandler(string source, Action<string, object[]> logger) {
			if (!string.IsNullOrWhiteSpace(source))
				throw new InvalidOperationException(
					"Does not require source");
		}

		public void ConfigureSourceProcessingStrategy(SourceDefinitionBuilder builder) {
			builder.FromAll();
			builder.AllEvents();
		}

		public void Load(string state) {
		}

		public void LoadShared(string state) {
			throw new NotImplementedException();
		}

		public void Initialize() {
		}

		public void InitializeShared() {
		}

		public string GetStatePartition(CheckpointTag eventPosition, string category, ResolvedEvent data) {
			throw new NotImplementedException();
		}

		public string TransformCatalogEvent(CheckpointTag eventPosition, ResolvedEvent data) {
			throw new NotImplementedException();
		}

		public bool ProcessEvent(
			string partition, CheckpointTag eventPosition, string category1, ResolvedEvent data,
			out string newState, out string newSharedState, out EmittedEventEnvelope[] emittedEvents) {
			newSharedState = null;
			emittedEvents = null;
			newState = null;
			return true;
		}

		public bool ProcessPartitionCreated(string partition, CheckpointTag createPosition, ResolvedEvent data,
			out EmittedEventEnvelope[] emittedEvents) {
			throw new NotImplementedException();
		}

		public bool ProcessPartitionDeleted(string partition, CheckpointTag deletePosition, out string newState) {
			throw new NotImplementedException();
		}

		public string TransformStateToResult() {
			throw new NotImplementedException();
		}

		public void Dispose() {
		}

		public IQuerySources GetSourceDefinition() {
			return SourceDefinitionBuilder.From(ConfigureSourceProcessingStrategy);
		}
	}
}
