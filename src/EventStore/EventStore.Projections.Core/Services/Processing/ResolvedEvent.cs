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
using System.Collections.Generic;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.TransactionLog.LogRecords;
using Newtonsoft.Json.Linq;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ResolvedEvent
    {
        private readonly string _eventStreamId;
        private readonly int _eventSequenceNumber;
        private readonly bool _resolvedLinkTo;

        private readonly string _positionStreamId;
        private readonly int _positionSequenceNumber;
        private readonly TFPos _position;
        private readonly TFPos _originalPosition;


        public readonly Guid EventId;
        public readonly string EventType;
        public readonly bool IsJson;
        public readonly DateTime Timestamp;

        public readonly string Data;
        public readonly string Metadata;
        public readonly string PositionMetadata;
        public readonly string StreamMetadata;
        public readonly bool IsLinkToDeletedStream;

        public ResolvedEvent(EventStore.Core.Data.ResolvedEvent resolvedEvent, byte[] streamMetadata)
        {
            var positionEvent = resolvedEvent.Link ?? resolvedEvent.Event;
            var @event = resolvedEvent.Event;
            _positionStreamId = positionEvent.EventStreamId;
            _positionSequenceNumber = positionEvent.EventNumber;
            _eventStreamId = @event != null ? @event.EventStreamId : null;
            _eventSequenceNumber = @event != null ? @event.EventNumber : -1;
            _resolvedLinkTo = positionEvent != @event;
            _position = resolvedEvent.OriginalPosition ?? new TFPos(-1, positionEvent.LogPosition);
            EventId = @event != null ? @event.EventId : Guid.Empty;
            EventType = @event != null ? @event.EventType : null;
            IsJson = @event != null && (@event.Flags & PrepareFlags.IsJson) != 0;
            Timestamp = positionEvent.TimeStamp;

            //TODO: handle utf-8 conversion exception
            Data = @event != null && @event.Data != null ? Helper.UTF8NoBom.GetString(@event.Data) : null;
            Metadata = @event != null && @event.Metadata != null ? Helper.UTF8NoBom.GetString(@event.Metadata) : null;
            PositionMetadata = _resolvedLinkTo
                ? (positionEvent.Metadata != null ? Helper.UTF8NoBom.GetString(positionEvent.Metadata) : null)
                : null;
            StreamMetadata = streamMetadata != null ? Helper.UTF8NoBom.GetString(streamMetadata) : null;

            TFPos originalPosition;
            if (_resolvedLinkTo)
            {
                Dictionary<string, JToken> extraMetadata = null;
                if (positionEvent.Metadata != null && positionEvent.Metadata.Length > 0)
                {
                    //TODO: parse JSON only when unresolved link and just tag otherwise
                    CheckpointTag tag;
                    if (resolvedEvent.Link != null && resolvedEvent.Event == null)
                    {
                        var checkpointTagJson =
                            positionEvent.Metadata.ParseCheckpointTagVersionExtraJson(default(ProjectionVersion));
                        tag = checkpointTagJson.Tag;
                        extraMetadata = checkpointTagJson.ExtraMetadata;
                    }
                    else
                    {
                        tag = positionEvent.Metadata.ParseCheckpointTagJson();
                    }
                    var parsedPosition = tag.Position;
                    originalPosition = parsedPosition != new TFPos(long.MinValue, long.MinValue)
                                           ? parsedPosition
                                           : new TFPos(-1, resolvedEvent.OriginalEvent.LogPosition);
                }
                else
                    originalPosition = new TFPos(-1, resolvedEvent.OriginalEvent.LogPosition);

                JToken deletedValue;
                if (resolvedEvent.ResolveResult == ReadEventResult.StreamDeleted
                    || resolvedEvent.ResolveResult == ReadEventResult.NoStream
                    || extraMetadata != null && extraMetadata.TryGetValue("$deleted", out deletedValue))
                {
                    IsLinkToDeletedStream = true;
                    var streamId = SystemEventTypes.StreamReferenceEventToStreamId(
                        SystemEventTypes.LinkTo, resolvedEvent.Link.Data);
                    _eventStreamId = streamId;
                }
            }
            else
            {
                originalPosition = resolvedEvent.OriginalPosition ?? new TFPos(-1, positionEvent.LogPosition); 
            }
            _originalPosition = originalPosition;

        }


        public ResolvedEvent(
            string positionStreamId, int positionSequenceNumber, string eventStreamId, int eventSequenceNumber,
            bool resolvedLinkTo, TFPos position, TFPos originalPosition, Guid eventId, string eventType, bool isJson, byte[] data,
            byte[] metadata, byte[] positionMetadata, byte[] streamMetadata, DateTime timestamp)
        {

            _positionStreamId = positionStreamId;
            _positionSequenceNumber = positionSequenceNumber;
            _eventStreamId = eventStreamId;
            _eventSequenceNumber = eventSequenceNumber;
            _resolvedLinkTo = resolvedLinkTo;
            _position = position;
            _originalPosition = originalPosition;
            EventId = eventId;
            EventType = eventType;
            IsJson = isJson;
            Timestamp = timestamp;

            //TODO: handle utf-8 conversion exception
            Data = data != null ? Helper.UTF8NoBom.GetString(data) : null;
            Metadata = metadata != null ? Helper.UTF8NoBom.GetString(metadata) : null;
            PositionMetadata = positionMetadata != null ? Helper.UTF8NoBom.GetString(positionMetadata) : null;
            StreamMetadata = streamMetadata != null ? Helper.UTF8NoBom.GetString(streamMetadata) : null;
        }


        public ResolvedEvent(
            string positionStreamId, int positionSequenceNumber, string eventStreamId, int eventSequenceNumber,
            bool resolvedLinkTo, TFPos position, Guid eventId, string eventType, bool isJson, string data,
            string metadata, string positionMetadata = null, string streamMetadata = null)
        {
            DateTime timestamp = default(DateTime);
            if (Guid.Empty == eventId)
                throw new ArgumentException("Empty eventId provided.");
            if (string.IsNullOrEmpty(eventType))
                throw new ArgumentException("Empty eventType provided.");

            _positionStreamId = positionStreamId;
            _positionSequenceNumber = positionSequenceNumber;
            _eventStreamId = eventStreamId;
            _eventSequenceNumber = eventSequenceNumber;
            _resolvedLinkTo = resolvedLinkTo;
            _position = position;
            EventId = eventId;
            EventType = eventType;
            IsJson = isJson;
            Timestamp = timestamp;

            Data = data;
            Metadata = metadata;
            PositionMetadata = positionMetadata;
            StreamMetadata = streamMetadata;
        }

        public string EventStreamId
        {
            get { return _eventStreamId; }
        }

        public int EventSequenceNumber
        {
            get { return _eventSequenceNumber; }
        }

        public bool ResolvedLinkTo
        {
            get { return _resolvedLinkTo; }
        }

        public string PositionStreamId
        {
            get { return _positionStreamId; }
        }

        public int PositionSequenceNumber
        {
            get { return _positionSequenceNumber; }
        }

        public TFPos Position
        {
            get { return _position; }
        }

        public TFPos OriginalPosition
        {
            get { return _originalPosition; }
        }
    }
}
