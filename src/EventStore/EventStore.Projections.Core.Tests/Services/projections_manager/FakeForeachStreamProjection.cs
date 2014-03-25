using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    public class FakeForeachStreamProjection : IProjectionStateHandler
    {
        private readonly string _query;
        private readonly Action<string> _logger;
        private string _state;

        public FakeForeachStreamProjection(string query, Action<string> logger)
        {
            _query = query;
            _logger = logger;
        }

        public void Dispose()
        {
        }

        public void ConfigureSourceProcessingStrategy(SourceDefinitionBuilder builder)
        {
            _logger("ConfigureSourceProcessingStrategy(" + builder + ")");
            builder.FromAll();
            builder.AllEvents();
            builder.SetByStream();
            builder.SetDefinesStateTransform();
        }

        public void Load(string state)
        {
            _logger("Load(" + state + ")");
            _state = state;
        }

        public void LoadShared(string state)
        {
            throw new NotImplementedException();
        }

        public void Initialize()
        {
            _logger("Initialize");
            _state = "";
        }

        public void InitializeShared()
        {
            _logger("InitializeShared");
            throw new NotImplementedException();
        }

        public string GetStatePartition(CheckpointTag eventPosition, string category, ResolvedEvent data)
        {
            _logger("GetStatePartition(" + "..." + ")");
            return @data.EventStreamId;
        }

        public string TransformCatalogEvent(CheckpointTag eventPosition, ResolvedEvent data)
        {
            throw new NotImplementedException();
        }

        public bool ProcessEvent(
            string partition, CheckpointTag eventPosition, string category1, ResolvedEvent data,
            out string newState, out string newSharedState, out EmittedEventEnvelope[] emittedEvents)
        {
            newSharedState = null;
            if (data.EventType == "fail" || _query == "fail")
                throw new Exception("failed");
            _logger("ProcessEvent(" + "..." + ")");
            newState = "{\"data\": " + _state + data + "}";
            emittedEvents = null;
            return true;
        }

        public bool ProcessPartitionCreated(string partition, CheckpointTag createPosition, ResolvedEvent data, out EmittedEventEnvelope[] emittedEvents)
        {
            _logger("Process ProcessPartitionCreated");
            emittedEvents = null;
            return false;
        }

        public bool ProcessPartitionDeleted(string partition, CheckpointTag deletePosition, out string newState)
        {
            throw new NotImplementedException();
        }

        public string TransformStateToResult()
        {
            return _state;
        }

        public IQuerySources GetSourceDefinition()
        {
            return SourceDefinitionBuilder.From(ConfigureSourceProcessingStrategy);
        }

    }
}
