using System;
using System.Collections.Generic;
using EventStore.Core.Services;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using EventStore.Common.Utils;

namespace EventStore.Projections.Core.Standard
{
    public class ByCorrelationId : IProjectionStateHandler, IProjectionCheckpointHandler
    {
        private readonly string _corrIdStreamPrefix;
        private readonly string _corrIdCheckpointStream;

        public ByCorrelationId(string source, Action<string, object[]> logger)
        {
            if (!string.IsNullOrWhiteSpace(source))
                throw new InvalidOperationException("Empty source expected");
            _corrIdStreamPrefix = "$bc-";
            _corrIdCheckpointStream = "$bc";
        }

        public void ConfigureSourceProcessingStrategy(SourceDefinitionBuilder builder)
        {
            builder.FromAll();
            builder.AllEvents();
            builder.SetIncludeLinks();
        }

        public void Load(string state)
        {
        }

        public void LoadShared(string state)
        {
            throw new NotImplementedException();
        }

        public void Initialize()
        {
        }

        public void InitializeShared()
        {
        }

        public string GetStatePartition(CheckpointTag eventPosition, string category, ResolvedEvent data)
        {
            throw new NotImplementedException();
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
            emittedEvents = null;
            newState = null;
            if (data.EventStreamId != data.PositionStreamId)
                return false;
            if(!data.IsJson)
                return false;

            JObject metadata = null;

            try{
                metadata = JObject.Parse(data.Metadata);
            }
            catch(JsonReaderException){
                return false;
            }

            if(metadata["$correlationId"] == null)
                return false;

            string correlationId = metadata["$correlationId"].Value<string>();
            if (correlationId == null)
                return false;

            string linkTarget;
            if (data.EventType == SystemEventTypes.LinkTo)
                linkTarget = data.Data;
            else
                linkTarget = data.EventSequenceNumber + "@" + data.EventStreamId;

            emittedEvents = new[]
            {
                new EmittedEventEnvelope(
                    new EmittedDataEvent(
                        _corrIdStreamPrefix + correlationId, Guid.NewGuid(), "$>", false,
                        linkTarget,
                        null, eventPosition, expectedTag: null))
            };


            return true;
        }

        public bool ProcessPartitionCreated(string partition, CheckpointTag createPosition, ResolvedEvent data, out EmittedEventEnvelope[] emittedEvents)
        {
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

        public void Dispose()
        {
        }

        public void ProcessNewCheckpoint(CheckpointTag checkpointPosition, out EmittedEventEnvelope[] emittedEvents)
        {
            emittedEvents = new[]
            {
                new EmittedEventEnvelope(
                    new EmittedDataEvent(
                        _corrIdCheckpointStream, Guid.NewGuid(), ProjectionEventTypes.PartitionCheckpoint,
                        true, checkpointPosition.ToJsonString(), null, checkpointPosition, expectedTag: null))
            };
        }

        public IQuerySources GetSourceDefinition()
        {
            return SourceDefinitionBuilder.From(ConfigureSourceProcessingStrategy);
        }

    }
}
