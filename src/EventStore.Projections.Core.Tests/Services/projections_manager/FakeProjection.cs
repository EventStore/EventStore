using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services.projections_manager {
	public class FakeProjection : IProjectionStateHandler {
		private readonly string _query;
		private readonly Action<string, object[]> _logger;

		public FakeProjection(string query, Action<string, object[]> logger) {
			_query = query;
			_logger = logger;
		}

		public void Dispose() {
		}

		private void Log(string msg, params object[] args) {
			_logger(msg, args);
		}

		public void ConfigureSourceProcessingStrategy(SourceDefinitionBuilder builder) {
			Log("ConfigureSourceProcessingStrategy(" + builder + ")");
			builder.FromAll();
			builder.AllEvents();
		}

		public void Load(string state) {
			Log("Load(" + state + ")");
			throw new NotImplementedException();
		}

		public void LoadShared(string state) {
			throw new NotImplementedException();
		}

		public void Initialize() {
			Log("Initialize");
		}

		public void InitializeShared() {
			Log("InitializeShared");
		}

		public string GetStatePartition(CheckpointTag eventPosition, string category, ResolvedEvent data) {
			Log("GetStatePartition(" + "..." + ")");
			throw new NotImplementedException();
		}

		public bool ProcessEvent(
			string partition, CheckpointTag eventPosition, string category1, ResolvedEvent data,
			out string newState, out string newSharedState, out EmittedEventEnvelope[] emittedEvents) {
			newSharedState = null;
			if (data.EventType == "fail" || _query == "fail")
				throw new Exception("failed");
			Log("ProcessEvent(" + "..." + ")");
			newState = "{\"data\": 1}";
			emittedEvents = null;
			return true;
		}

		public bool ProcessPartitionCreated(string partition, CheckpointTag createPosition, ResolvedEvent data,
			out EmittedEventEnvelope[] emittedEvents) {
			Log("Process ProcessPartitionCreated");
			emittedEvents = null;
			return false;
		}

		public bool ProcessPartitionDeleted(string partition, CheckpointTag deletePosition, out string newState) {
			throw new NotImplementedException();
		}

		public string TransformStateToResult() {
			throw new NotImplementedException();
		}

		public IQuerySources GetSourceDefinition() {
			return SourceDefinitionBuilder.From(ConfigureSourceProcessingStrategy);
		}
	}
}
