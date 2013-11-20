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

namespace EventStore.Projections.Core.Services.Processing
{
    public class ResultEventEmitter : IResultEventEmitter
    {
        private readonly ProjectionNamesBuilder _namesBuilder;

        private readonly EmittedStream.WriterConfiguration.StreamMetadata _resultStreamMetadata =
            new EmittedStream.WriterConfiguration.StreamMetadata(/* TBD */);

        public ResultEventEmitter(ProjectionNamesBuilder namesBuilder)
        {
            if (namesBuilder == null) throw new ArgumentNullException("namesBuilder");
            _namesBuilder = namesBuilder;
        }

        public EmittedEventEnvelope[] ResultUpdated(string partition, string result, CheckpointTag at)
        {
            return CreateResultUpdatedEvents(partition, result, at);
        }

        private EmittedEventEnvelope[] CreateResultUpdatedEvents(string partition, string projectionResult, CheckpointTag at)
        {
            var streamId = _namesBuilder.MakePartitionResultStreamName(partition);
            var allResultsStreamId = _namesBuilder.GetResultStreamName();
            if (string.IsNullOrEmpty(partition))
            {
                var result =
                    new EmittedEventEnvelope(
                        projectionResult == null
                            ? new EmittedDataEvent(
                                streamId, Guid.NewGuid(), "ResultRemoved", true, null, null, at, null)
                            : new EmittedDataEvent(
                                streamId, Guid.NewGuid(), "Result", true, projectionResult, null, at, null),
                        _resultStreamMetadata);
                
                return new[] {result};
            }
            else
            {
                var linkTo = new EmittedLinkTo(allResultsStreamId, Guid.NewGuid(), streamId, at, null);
                var linkToEnvelope = new EmittedEventEnvelope(linkTo, _resultStreamMetadata);
                var result =
                    new EmittedEventEnvelope(
                        projectionResult == null
                            ? new EmittedDataEvent(
                                streamId, Guid.NewGuid(), "ResultRemoved", true, null, null, at, null,
                                linkTo.SetTargetEventNumber)
                            : new EmittedDataEvent(
                                streamId, Guid.NewGuid(), "Result", true, projectionResult, null, at, null,
                                linkTo.SetTargetEventNumber), _resultStreamMetadata);
                return new[] {result, linkToEnvelope};
            }
        }
    }
}
