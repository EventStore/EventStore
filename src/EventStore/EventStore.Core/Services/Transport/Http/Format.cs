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
using System.Diagnostics;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Transport.Http;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http
{
    public static class Format
    {
        public static class Atom
        {
            public static string ListStreamsCompletedServiceDoc(HttpEntity entity, Message message)
            {
                Debug.Assert(message.GetType() == typeof(ClientMessage.ListStreamsCompleted));

                var streams = message as ClientMessage.ListStreamsCompleted;
                return streams != null
                           ? entity.ResponseCodec.To(Convert.ToServiceDocument(streams.Streams,
                                                                               new string[0],
                                                                               entity.UserHostName))
                           : string.Empty;
            }

            public static string ReadEventCompletedEntry(HttpEntity entity, Message message)
            {
                Debug.Assert(message.GetType() == typeof(ClientMessage.ReadEventCompleted));

                var completed = message as ClientMessage.ReadEventCompleted;
                if (completed != null)
                {
                    switch (completed.Result)
                    {
                        case SingleReadResult.Success:
                            return entity.ResponseCodec.To(Convert.ToEntry(completed.Record, entity.UserHostName));
                        case SingleReadResult.NotFound:
                        case SingleReadResult.NoStream:
                        case SingleReadResult.StreamDeleted:
                            return string.Empty;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                return string.Empty;
            }

            public static string ReadEventsBackwardsCompletedFeed(HttpEntity entity, Message message, int start, int count)
            {
                Debug.Assert(message.GetType() == typeof(ClientMessage.ReadEventsBackwardsCompleted));

                var completed = message as ClientMessage.ReadEventsBackwardsCompleted;
                if (completed != null)
                {
                    switch (completed.Result)
                    {
                        case RangeReadResult.Success:
                            var updateTime = completed.Events.Length != 0
                                                 ? completed.Events[0].TimeStamp
                                                 : DateTime.MinValue.ToUniversalTime();
                            return entity.ResponseCodec.To(Convert.ToFeed(completed.EventStreamId,
                                                                          start,
                                                                          count,
                                                                          updateTime,
                                                                          completed.Events,
                                                                          Convert.ToEntry,
                                                                          entity.UserHostName));
                        case RangeReadResult.NoStream:
                        case RangeReadResult.StreamDeleted:
                            return string.Empty;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                return string.Empty;
            }

            public static string CreateStreamCompleted(HttpEntity entity, Message message)
            {
                Debug.Assert(message.GetType() == typeof(ClientMessage.CreateStreamCompleted));
                return string.Empty;
            }

            public static string DeleteStreamCompleted(HttpEntity entity, Message message)
            {
                Debug.Assert(message.GetType() == typeof(ClientMessage.DeleteStreamCompleted));
                return string.Empty;
            }
        }

        public static string TextMessage(HttpEntity entity, Message message)
        {
            Debug.Assert(message.GetType() == typeof(HttpMessage.TextMessage));

            var textMessage = message as HttpMessage.TextMessage;
            return textMessage != null ? entity.ResponseCodec.To(textMessage) : string.Empty;
        }

        public static string WriteEvents(ICodec codec, Message message)
        {
            Debug.Assert(message.GetType() == typeof (ClientMessage.WriteEvents));

            var writeEvents = message as ClientMessage.WriteEvents;
            if (writeEvents == null)
                return string.Empty;

            return codec.To(new ClientMessageDto.WriteEventText(writeEvents.CorrelationId,
                                                                writeEvents.ExpectedVersion,
                                                                writeEvents.Events.Select(e => new ClientMessageDto.EventText(e.EventId, 
                                                                                                                              e.EventType,
                                                                                                                              e.Data, 
                                                                                                                              e.Metadata)).ToArray()));
        }

        public static string WriteEventsCompleted(HttpEntity entity, Message message)
        {
            Debug.Assert(message.GetType() == typeof(ClientMessage.WriteEventsCompleted));
            return string.Empty;
        }

        public static string ReadEventCompleted(HttpEntity entity, Message message)
        {
            Debug.Assert(message.GetType() == typeof(ClientMessage.ReadEventCompleted));

            var completed = message as ClientMessage.ReadEventCompleted;
            if (completed != null)
            {
                switch (completed.Result)
                {
                    case SingleReadResult.Success:
                        return EventConvertion.ConvertOnRead(completed, entity.ResponseCodec);
                    case SingleReadResult.NotFound:
                    case SingleReadResult.NoStream:
                    case SingleReadResult.StreamDeleted:
                        return string.Empty;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            return string.Empty;
        }

        public static string GetFreshStatsCompleted(HttpEntity entity, Message message)
        {
            Debug.Assert(message.GetType() == typeof(MonitoringMessage.GetFreshStatsCompleted));

            var completed = message as MonitoringMessage.GetFreshStatsCompleted;
            if (completed == null || !completed.Success)
                return string.Empty;

            return entity.ResponseCodec.To(completed.Stats);
        }
    }
}
