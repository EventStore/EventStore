// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Services
{
    public interface ISourceDefinitionSource
    {
        IQuerySources GetSourceDefinition();
    }


    public interface IProjectionStateHandler : IDisposable, ISourceDefinitionSource
    {
        void Load(string state);
        void LoadShared(string state);
        void Initialize();

        /// <summary>
        /// Get state partition from the event
        /// </summary>
        /// <returns>partition name</returns>
        string GetStatePartition(
            CheckpointTag eventPosition, string category, ResolvedEvent data);

        /// <summary>
        /// Processes event and updates internal state if necessary.  
        /// </summary>
        /// <returns>true - if event was processed (new state must be returned) </returns>
        bool ProcessEvent(
            string partition, CheckpointTag eventPosition, string category, ResolvedEvent data, out string newState,
            out string newSharedState, out EmittedEventEnvelope[] emittedEvents);

        /// <summary>
        /// Transforms current state into a projection result.  Should not call any emit/linkTo etc 
        /// </summary>
        /// <returns>result JSON or NULL if current state has been skipped</returns>
        string TransformStateToResult();
    }

    public interface IProjectionCheckpointHandler
    {
        void ProcessNewCheckpoint(CheckpointTag checkpointPosition, out EmittedEventEnvelope[] emittedEvents);
    }

    public static class ProjectionStateHandlerTestExtensions
    {
        public static bool ProcessEvent(
            this IProjectionStateHandler self, string partition, CheckpointTag eventPosition, string streamId,
            string eventType, string category, Guid eventId, int eventSequenceNumber, string metadata, string data,
            out string state, out EmittedEventEnvelope[] emittedEvents, bool isJson = true)
        {
            string ignoredSharedState;
            return self.ProcessEvent(
                partition, eventPosition, category,
                new ResolvedEvent(
                    streamId, eventSequenceNumber, streamId, eventSequenceNumber, false, new TFPos(0, -1), eventId,
                    eventType, isJson, data, metadata), out state, out ignoredSharedState, out emittedEvents);
        }

        public static bool ProcessEvent(
            this IProjectionStateHandler self, string partition, CheckpointTag eventPosition, string streamId,
            string eventType, string category, Guid eventId, int eventSequenceNumber, string metadata, string data,
            out string state, out string sharedState, out EmittedEventEnvelope[] emittedEvents, bool isJson = true)
        {
            return self.ProcessEvent(
                partition, eventPosition, category,
                new ResolvedEvent(
                    streamId, eventSequenceNumber, streamId, eventSequenceNumber, false, new TFPos(0, -1), eventId,
                    eventType, isJson, data, metadata), out state, out sharedState, out emittedEvents);
        }

        public static string GetNativeHandlerName(this Type handlerType)
        {
            return "native:" + handlerType.Namespace + "." + handlerType.Name;
        }

    }

}
