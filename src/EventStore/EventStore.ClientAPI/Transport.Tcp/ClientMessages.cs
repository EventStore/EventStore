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
using System.Net;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;
using ProtoBuf;

namespace EventStore.ClientAPI.Transport.Tcp
{
    internal static class ClientMessages
    {
        [ProtoContract]
        internal class DeniedToRoute
        {
            [ProtoMember(1)]
            public string ExternalTcpAddress { get; set; }

            [ProtoMember(2)]
            public int ExternalTcpPort { get; set; }

            [ProtoMember(3)]
            public string ExternalHttpAddress { get; set; }

            [ProtoMember(4)]
            public int ExternalHttpPort { get; set; }

            public IPEndPoint ExternalTcpEndPoint
            {
                get
                {
                    return new IPEndPoint(IPAddress.Parse(ExternalTcpAddress), ExternalTcpPort);
                }
            }

            public IPEndPoint ExternalHttpEndPoint
            {
                get
                {
                    return new IPEndPoint(IPAddress.Parse(ExternalHttpAddress), ExternalHttpPort);
                }
            }

            public DeniedToRoute()
            {
            }

            public DeniedToRoute(IPEndPoint externalTcpEndPoint, IPEndPoint externalHttpEndPoint)
            {
                ExternalTcpAddress = externalTcpEndPoint.Address.ToString();
                ExternalTcpPort = externalTcpEndPoint.Port;

                ExternalHttpAddress = externalHttpEndPoint.Address.ToString();
                ExternalHttpPort = externalHttpEndPoint.Port;
            }
        }

        [ProtoContract]
        internal class CreateStream
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2, IsRequired = false)]
            public byte[] Metadata { get; set; }

            [ProtoMember(3)]
            public bool AllowForwarding { get; set; }

            public CreateStream()
            {
            }

            public CreateStream(string eventStreamId, byte[] metadata, bool allowForwarding = true)
            {
                Ensure.NotNull(eventStreamId, "streamId");

                EventStreamId = eventStreamId;
                Metadata = metadata;
                AllowForwarding = allowForwarding;
            }
        }

        [ProtoContract]
        internal class CreateStreamCompleted
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int ErrorCode { get; set; }

            [ProtoMember(3)]
            public string Error { get; set; }

            public CreateStreamCompleted()
            {
            }

            public CreateStreamCompleted(string eventStreamId, OperationErrorCode errorCode, string error)
            {
                EventStreamId = eventStreamId;
                ErrorCode = (int)errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        internal class Event
        {
            [ProtoMember(1)]
            public byte[] EventId { get; set; }

            [ProtoMember(2, IsRequired = false)]
            public string EventType { get; set; }

            [ProtoMember(3)]
            public byte[] Data { get; set; }

            [ProtoMember(4, IsRequired = false)]
            public byte[] Metadata { get; set; }

            public Event()
            {
            }

            public Event(Guid eventId, string eventType, byte[] data, byte[] metadata)
            {
                EventId = eventId.ToByteArray();
                EventType = eventType;
                Data = data;
                Metadata = metadata;
            }
        }

        [ProtoContract]
        internal class WriteEvents
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int ExpectedVersion { get; set; }

            [ProtoMember(3)]
            public Event[] Events { get; set; }

            [ProtoMember(4)]
            public bool AllowForwarding { get; set; }

            public WriteEvents()
            {
            }

            public WriteEvents(string eventStreamId, 
                               int expectedVersion, 
                               Event[] events, 
                               bool allowForwarding = true)
            {
                Ensure.NotNull(events, "events");
                Ensure.Positive(events.Length, "events.Length");

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
                Events = events;
                AllowForwarding = allowForwarding;
            }
        }

        [ProtoContract]
        internal class WriteEventsCompleted
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int ErrorCode { get; set; }

            [ProtoMember(3)]
            public string Error { get; set; }

            [ProtoMember(4)]
            public int EventNumber { get; set; }

            public WriteEventsCompleted()
            {
            }

            public WriteEventsCompleted(string eventStreamId, OperationErrorCode errorCode, string error, int eventNumber)
            {
                EventStreamId = eventStreamId;
                ErrorCode = (int)errorCode;
                Error = error;
                EventNumber = eventNumber;
            }
        }

        [ProtoContract]
        internal class DeleteStream
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int ExpectedVersion { get; set; }

            [ProtoMember(3)]
            public bool AllowForwarding { get; set; }

            public DeleteStream()
            {
            }

            public DeleteStream(string eventStreamId, 
                                int expectedVersion, 
                                bool allowForwarding = true)
            {
                Ensure.NotNull(eventStreamId, "streamId");

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
                AllowForwarding = allowForwarding;
            }
        }

        [ProtoContract]
        internal class DeleteStreamCompleted
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int ErrorCode { get; set; }

            [ProtoMember(3)]
            public string Error { get; set; }

            public DeleteStreamCompleted()
            {
            }

            public DeleteStreamCompleted(string eventStreamId, OperationErrorCode errorCode, string error)
            {
                EventStreamId = eventStreamId;
                ErrorCode = (int)errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        internal class ReadEvent
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int EventNumber { get; set; }

            [ProtoMember(3)]
            public bool ResolveLinktos { get; set; }

            public ReadEvent()
            {
            }

            public ReadEvent(string eventStreamId, int eventNumber, bool resolveLinkTos)
            {
                EventStreamId = eventStreamId;
                EventNumber = eventNumber;
                ResolveLinktos = resolveLinkTos;
            }
        }

        [ProtoContract]
        internal class ReadEventCompleted
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int EventNumber { get; set; }

            [ProtoMember(3)]
            public int Result { get; set; }

            [ProtoMember(4)]
            public string EventType { get; set; }

            [ProtoMember(5)]
            public byte[] Data { get; set; }

            [ProtoMember(6)]
            public byte[] Metadata { get; set; }

            [ProtoMember(7)]
            public long LogPosition { get; set; }

            public ReadEventCompleted()
            {
            }

            public ReadEventCompleted(string eventStreamId,
                                      int eventNumber,
                                      SingleReadResult result,
                                      string eventType,
                                      byte[] data,
                                      byte[] metadata, long logPosition)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "streamId");
                Ensure.Nonnegative(eventNumber, "eventNumber");
                if (result == SingleReadResult.Success)
                    Ensure.NotNull(data, "data");

                EventStreamId = eventStreamId;
                EventNumber = eventNumber;
                Result = (int)result;
                EventType = eventType;
                Data = data;
                Metadata = metadata;
                LogPosition = logPosition;
            }
        }

        [ProtoContract]
        internal class ReadStreamEventsForward
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int StartIndex { get; set; }

            [ProtoMember(3)]
            public int MaxCount { get; set; }

            [ProtoMember(4)]
            public bool ResolveLinkTos { get; set; }

            [ProtoMember(5)]
            public bool ReturnLastEventNumber { get; set; }

            public ReadStreamEventsForward()
            {
            }

            public ReadStreamEventsForward(string eventStreamId, int startIndex, int maxCount, bool resolveLinkTos)
            {
                EventStreamId = eventStreamId;
                StartIndex = startIndex;
                MaxCount = maxCount;
                ResolveLinkTos = resolveLinkTos;
            }
        }

        [ProtoContract]
        internal class ReadStreamEventsForwardCompleted
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public EventLinkPair[] Events { get; set; }

            [ProtoMember(3)]
            public int Result { get; set; }

            [ProtoMember(4)]
            public long? LastCommitPosition { get; set; }

            [ProtoMember(5)]
            public int LastEventNumber { get; set; }

            public ReadStreamEventsForwardCompleted()
            {
            }

            public ReadStreamEventsForwardCompleted(string eventStreamId,
                                                    EventLinkPair[] events,
                                                    RangeReadResult result,
                                                    long? lastCommitPosition,
                                                    int lastEventNumber)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "streamId");

                EventStreamId = eventStreamId;
                Events = events;
                Result = (int)result;
                LastCommitPosition = lastCommitPosition;
                LastEventNumber = lastEventNumber;
            }
        }

        [ProtoContract]
        internal class ReadStreamEventsBackward
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int StartIndex { get; set; }

            [ProtoMember(3)]
            public int MaxCount { get; set; }

            [ProtoMember(4)]
            public bool ResolveLinkTos { get; set; }

            public ReadStreamEventsBackward()
            {
            }

            public ReadStreamEventsBackward(string eventStreamId, int startIndex, int maxCount, bool resolveLinkTos)
            {
                EventStreamId = eventStreamId;
                StartIndex = startIndex;
                MaxCount = maxCount;
                ResolveLinkTos = resolveLinkTos;
            }
        }

        [ProtoContract]
        internal class ReadStreamEventsBackwardCompleted
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public EventLinkPair[] Events { get; set; }

            [ProtoMember(3)]
            public int Result { get; set; }

            [ProtoMember(4)]
            public long? LastCommitPosition { get; set; }

            public ReadStreamEventsBackwardCompleted()
            {
            }

            public ReadStreamEventsBackwardCompleted(string eventStreamId,
                                                     EventLinkPair[] events,
                                                     RangeReadResult result,
                                                     long? lastCommitPosition)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "streamId");

                EventStreamId = eventStreamId;
                Events = events;
                Result = (int)result;
                LastCommitPosition = lastCommitPosition;
            }
        }

        [ProtoContract]
        internal class ReadAllEventsForward
        {
            [ProtoMember(1)]
            public long CommitPosition { get; set; }

            [ProtoMember(2)]
            public long PreparePosition { get; set; }

            [ProtoMember(3)]
            public int MaxCount { get; set; }

            [ProtoMember(4)]
            public bool ResolveLinktos { get; set; }

            public ReadAllEventsForward()
            {
            }

            public ReadAllEventsForward(long commitPosition, long preparePosition, int maxCount, bool resolveLinktos)
            {
                CommitPosition = commitPosition;
                PreparePosition = preparePosition;
                MaxCount = maxCount;
                ResolveLinktos = resolveLinktos;
            }
        }

        [ProtoContract]
        internal class ReadAllEventsForwardCompleted
        {
            [ProtoMember(1)]
            public long CommitPosition { get; set; }

            [ProtoMember(2)]
            public long PreparePosition { get; set; }

            [ProtoMember(3)]
            public EventLinkPair[] Events { get; set; }

            [ProtoMember(4)]
            public long NextCommitPosition { get; set; }

            [ProtoMember(5)]
            public long NextPreparePosition { get; set; }

            public ReadAllEventsForwardCompleted()
            {
            }

            public ReadAllEventsForwardCompleted(long commitPosition,
                                                 long preparePosition,
                                                 EventLinkPair[] events,
                                                 long nextCommitPosition,
                                                 long nextPreparePosition)
            {
                CommitPosition = commitPosition;
                PreparePosition = preparePosition;
                Events = events;
                NextCommitPosition = nextCommitPosition;
                NextPreparePosition = nextPreparePosition;
            }
        }


        [ProtoContract]
        internal class ReadAllEventsBackward
        {
            [ProtoMember(1)]
            public long CommitPosition { get; set; }

            [ProtoMember(2)]
            public long PreparePosition { get; set; }

            [ProtoMember(3)]
            public int MaxCount { get; set; }

            [ProtoMember(4)]
            public bool ResolveLinkTos { get; set; }

            public ReadAllEventsBackward()
            {
            }

            public ReadAllEventsBackward(long commitPosition, long preparePosition, int maxCount, bool resolveLinktos)
            {
                CommitPosition = commitPosition;
                PreparePosition = preparePosition;
                MaxCount = maxCount;
                ResolveLinkTos = resolveLinktos;
            }
        }

        [ProtoContract]
        internal class ReadAllEventsBackwardCompleted
        {
            [ProtoMember(1)]
            public long CommitPosition { get; set; }

            [ProtoMember(2)]
            public long PreparePosition { get; set; }

            [ProtoMember(3)]
            public EventLinkPair[] Events { get; set; }

            [ProtoMember(4)]
            public long NextCommitPosition { get; set; }

            [ProtoMember(5)]
            public long NextPreparePosition { get; set; }

            public ReadAllEventsBackwardCompleted()
            {
            }

            public ReadAllEventsBackwardCompleted(long commitPosition,
                                                  long preparePosition,
                                                  EventLinkPair[] events,
                                                  long nextCommitPosition,
                                                  long nextPreparePosition)
            {
                CommitPosition = commitPosition;
                PreparePosition = preparePosition;
                Events = events;
                NextCommitPosition = nextCommitPosition;
                NextPreparePosition = nextPreparePosition;
            }
        }

        [ProtoContract]
        internal class TransactionStart
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int ExpectedVersion { get; set; }

            [ProtoMember(3)]
            public bool AllowForwarding { get; set; }

            public TransactionStart()
            {
            }

            public TransactionStart(string eventStreamId, 
                                    int expectedVersion,
                                    bool allowForwarding = true)
            {
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
                AllowForwarding = allowForwarding;
            }
        }

        [ProtoContract]
        internal class TransactionStartCompleted
        {
            [ProtoMember(1)]
            public long TransactionId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public int ErrorCode { get; set; }

            [ProtoMember(4)]
            public string Error { get; set; }

            public TransactionStartCompleted()
            {
            }

            public TransactionStartCompleted(long transactionId,
                                             string eventStreamId,
                                             OperationErrorCode errorCode,
                                             string error)
            {
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                ErrorCode = (int)errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        internal class TransactionWrite
        {
            [ProtoMember(1)]
            public long TransactionId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public Event[] Events { get; set; }

            [ProtoMember(4)]
            public bool AllowForwarding { get; set; }

            public TransactionWrite()
            {
            }

            public TransactionWrite(long transactionId, 
                                    string eventStreamId, 
                                    Event[] events,
                                    bool allowForwarding = true)
            {
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                Events = events;
                AllowForwarding = allowForwarding;
            }
        }

        [ProtoContract]
        internal class TransactionWriteCompleted
        {
            [ProtoMember(1)]
            public long TransactionId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public int ErrorCode { get; set; }

            [ProtoMember(4)]
            public string Error { get; set; }

            public TransactionWriteCompleted()
            {
            }

            public TransactionWriteCompleted(long transactionId, string eventStreamId, OperationErrorCode errorCode, string error)
            {
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                ErrorCode = (int)errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        internal class TransactionCommit
        {
            [ProtoMember(1)]
            public long TransactionId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public bool AllowForwarding { get; set; }

            public TransactionCommit()
            {
            }

            public TransactionCommit(long transactionId, 
                                     string eventStreamId, 
                                     bool allowForwarding = true)
            {
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                AllowForwarding = allowForwarding;
            }
        }

        [ProtoContract]
        internal class TransactionCommitCompleted
        {
            [ProtoMember(1)]
            public long TransactionId;

            [ProtoMember(2)]
            public int ErrorCode;

            [ProtoMember(3)]
            public string Error;

            public TransactionCommitCompleted()
            {
            }

            public TransactionCommitCompleted(long transactionId, OperationErrorCode errorCode, string error)
            {
                TransactionId = transactionId;
                ErrorCode = (int)errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        internal class SubscribeToStream
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            public SubscribeToStream()
            {
            }

            public SubscribeToStream(string eventStreamId)
            {
                EventStreamId = eventStreamId;
            }
        }

        [ProtoContract]
        internal class UnsubscribeFromStream
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            public UnsubscribeFromStream()
            {
            }

            public UnsubscribeFromStream(string eventStreamId)
            {
                EventStreamId = eventStreamId;
            }
        }

        [ProtoContract]
        internal class SubscribeToAllStreams
        {
        }

        [ProtoContract]
        internal class UnsubscribeFromAllStreams
        {
        }

        [ProtoContract]
        internal class StreamEventAppeared
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int EventNumber { get; set; }

            [ProtoMember(3)]
            public byte[] EventId { get; set; }

            [ProtoMember(4)]
            public string EventType { get; set; }

            [ProtoMember(5)]
            public byte[] Data { get; set; }

            [ProtoMember(6)]
            public byte[] Metadata { get; set; }
        }

        [ProtoContract]
        internal class SubscriptionDropped
        {
            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            public SubscriptionDropped()
            {
            }

            public SubscriptionDropped(string eventStreamId)
            {
                EventStreamId = eventStreamId;
            }
        }

        [ProtoContract]
        internal class SubscriptionToAllDropped
        {
        }
    }
}