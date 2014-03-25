using System;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    public class FakeBiStateProjection : IProjectionStateHandler
    {
        private readonly string _query;
        private readonly Action<string> _logger;

        public FakeBiStateProjection(string query, Action<string> logger)
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
            builder.SetIsBiState(true);
        }

        public void Load(string state)
        {
            _logger("Load(" + state + ")");
        }

        public void LoadShared(string state)
        {
            _logger("LoadShared(" + state + ")");
        }

        public void Initialize()
        {
            _logger("Initialize");
        }

        public void InitializeShared()
        {
            _logger("InitializeShared");
        }

        public string GetStatePartition(CheckpointTag eventPosition, string category, ResolvedEvent data)
        {
            _logger("GetStatePartition(" + "..." + ")");
            throw new NotImplementedException();
        }

        public string TransformCatalogEvent(CheckpointTag eventPosition, ResolvedEvent data)
        {
            throw new NotImplementedException();
        }

        public bool ProcessEvent(
            string partition, CheckpointTag eventPosition, string category1, ResolvedEvent data, out string newState,
            out string newSharedState, out EmittedEventEnvelope[] emittedEvents)
        {
            newSharedState = null;
            if (data.EventType == "fail" || _query == "fail")
                throw new Exception("failed");
            _logger("ProcessEvent(" + "..." + ")");
            newState = "{\"data\": 1}";
            newSharedState = "{\"data\": 2}";
            emittedEvents = null;
            return true;
        }

        public bool ProcessPartitionCreated(string partition, CheckpointTag createPosition, ResolvedEvent data, out EmittedEventEnvelope[] emittedEvents)
        {
            _logger("ProcessPartitionCreated");
            emittedEvents = null;
            return false;
        }

        public bool ProcessPartitionDeleted(string partition, CheckpointTag deletePosition, out string newState)
        {
            throw new NotImplementedException();
        }

        public string TransformStateToResult()
        {
            throw new NotImplementedException();
        }

        public IQuerySources GetSourceDefinition()
        {
            return SourceDefinitionBuilder.From(ConfigureSourceProcessingStrategy);
        }
    }
}
