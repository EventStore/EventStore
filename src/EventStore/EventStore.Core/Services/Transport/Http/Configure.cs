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
using System.Diagnostics;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Atom;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http
{
    public static class Configure
    {
        private const int MaxPossibleAge = 1;//TODO : TR change to 31556926 after tested;
        private const int MinPossibleAge = 1;

        public static ResponseConfiguration Ok(HttpEntity entity, Message message)
        {
            return new ResponseConfiguration(HttpStatusCode.OK, "OK", entity.ResponseCodec.ContentType);
        }

        public static ResponseConfiguration NotFound(HttpEntity entity, Message message)
        {
            return new ResponseConfiguration(HttpStatusCode.NotFound, "Not Found", null);
        }

        public static ResponseConfiguration Gone(HttpEntity entity, Message message)
        {
            return new ResponseConfiguration(HttpStatusCode.Gone, "Deleted", null);
        }

        public static ResponseConfiguration InternalServerEror(HttpEntity entity, Message message)
        {
            return new ResponseConfiguration(HttpStatusCode.InternalServerError, "Internal Server Error", null);
        }

        public static ResponseConfiguration ReadEventCompleted(HttpEntity entity, Message message)
        {
            Debug.Assert(message.GetType() == typeof(ClientMessage.ReadEventCompleted));

            var completed = message as ClientMessage.ReadEventCompleted;
            if (completed == null)
                return InternalServerEror(entity, message);

            switch (completed.Result)
            {
                case SingleReadResult.Success:
                    return new ResponseConfiguration(HttpStatusCode.OK, 
                                                     "OK",
                                                     entity.ResponseCodec.ContentType,
                                                     new KeyValuePair<string, string>(
                                                         "Cache-Control",
                                                         string.Format("max-age={0}", MaxPossibleAge)));
                case SingleReadResult.NotFound:
                case SingleReadResult.NoStream:
                    return NotFound(entity, completed);
                case SingleReadResult.StreamDeleted:
                    return Gone(entity, completed);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static ResponseConfiguration ReadEventsFromEndCompleted(HttpEntity entity, Message message)
        {
            Debug.Assert(message.GetType() == typeof(ClientMessage.ReadEventsBackwardsCompleted));

            var completed = message as ClientMessage.ReadEventsBackwardsCompleted;
            if (completed == null)
                return InternalServerEror(entity, message);

            var startIdx = (int)entity.Manager.AsyncState;
            var age = startIdx < AtomSpecs.FeedPageSize ? MinPossibleAge : MaxPossibleAge;

            switch (completed.Result)
            {
                case RangeReadResult.Success:
                    return new ResponseConfiguration(HttpStatusCode.OK,
                                                     "OK",
                                                     entity.ResponseCodec.ContentType,
                                                     new KeyValuePair<string, string>(
                                                         "Cache-Control", 
                                                         string.Format("max-age={0}", age)));
                case RangeReadResult.NoStream:
                    return NotFound(entity, completed);
                case RangeReadResult.StreamDeleted:
                    return Gone(entity, completed);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static ResponseConfiguration WriteEventsCompleted(HttpEntity entity, Message message)
        {
            Debug.Assert(message.GetType() == typeof(ClientMessage.WriteEventsCompleted));

            var completed = message as ClientMessage.WriteEventsCompleted;
            if (completed == null)
                return InternalServerEror(entity, message);

            switch (completed.ErrorCode)
            {
                case OperationErrorCode.Success:
                    return new ResponseConfiguration(HttpStatusCode.Created,
                                                     "Created",
                                                     null,
                                                     new KeyValuePair<string, string>("Location",
                                                                                      HostName.Combine(entity.UserHostName,
                                                                                                  "/streams/{0}/{1}",
                                                                                                  completed.EventStreamId,
                                                                                                  completed.EventNumber == 0 ? 1 : completed.EventNumber)));
                case OperationErrorCode.PrepareTimeout:
                case OperationErrorCode.CommitTimeout:
                case OperationErrorCode.ForwardTimeout:
                    return new ResponseConfiguration(HttpStatusCode.InternalServerError, "Write timeout", null);
                case OperationErrorCode.WrongExpectedVersion:
                    return new ResponseConfiguration(HttpStatusCode.BadRequest, "Wrong expected version", null);
                case OperationErrorCode.StreamDeleted:
                    return new ResponseConfiguration(HttpStatusCode.Gone, "Stream deleted", null);
                case OperationErrorCode.InvalidTransaction:
                    return new ResponseConfiguration(HttpStatusCode.InternalServerError, "Invalid transaction", null);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static ResponseConfiguration GetFreshStatsCompleted(HttpEntity entity, Message message)
        {
            Debug.Assert(message.GetType() == typeof(MonitoringMessage.GetFreshStatsCompleted));

            var completed = message as MonitoringMessage.GetFreshStatsCompleted;
            if (completed == null)
                return InternalServerEror(entity, message);

            return completed.Success ? Ok(entity, completed) : NotFound(entity, message);
        }

        public static ResponseConfiguration CreateStreamCompleted(HttpEntity entity, Message message)
        {
            Debug.Assert(message.GetType() == typeof(ClientMessage.CreateStreamCompleted));

            var completed = message as ClientMessage.CreateStreamCompleted;
            if (completed == null)
                return InternalServerEror(entity, message);

            switch (completed.ErrorCode)
            {
                case OperationErrorCode.Success:
                    return new ResponseConfiguration(HttpStatusCode.Created, 
                                                     "Stream created", 
                                                     null,
                                                     new KeyValuePair<string, string>("Location", 
                                                                                      HostName.Combine(entity.UserHostName, 
                                                                                                  "/streams/{0}", 
                                                                                                  completed.EventStreamId)));
                case OperationErrorCode.PrepareTimeout:
                case OperationErrorCode.CommitTimeout:
                case OperationErrorCode.ForwardTimeout:
                    return new ResponseConfiguration(HttpStatusCode.InternalServerError, "Create timeout", null);
                case OperationErrorCode.WrongExpectedVersion:
                case OperationErrorCode.StreamDeleted:
                case OperationErrorCode.InvalidTransaction:
                    return new ResponseConfiguration(HttpStatusCode.BadRequest,
                                                     string.Format("Error code : {0}. Reason : {1}", completed.ErrorCode, completed.Error),
                                                     null);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static ResponseConfiguration DeleteStreamCompleted(HttpEntity entity, Message message)
        {
            Debug.Assert(message.GetType() == typeof(ClientMessage.DeleteStreamCompleted));

            var completed = message as ClientMessage.DeleteStreamCompleted;
            if (completed == null)
                return InternalServerEror(entity, message);

            switch (completed.ErrorCode)
            {
                case OperationErrorCode.Success:
                    return new ResponseConfiguration(HttpStatusCode.NoContent, "Stream deleted", null);
                case OperationErrorCode.PrepareTimeout:
                case OperationErrorCode.CommitTimeout:
                case OperationErrorCode.ForwardTimeout:
                    return new ResponseConfiguration(HttpStatusCode.InternalServerError, "Delete timeout", null);
                case OperationErrorCode.WrongExpectedVersion:
                case OperationErrorCode.StreamDeleted:
                case OperationErrorCode.InvalidTransaction:
                    return new ResponseConfiguration(HttpStatusCode.BadRequest,
                                                     string.Format("Error code : {0}. Reason : {1}", completed.ErrorCode, completed.Error),
                                                     null);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static ResponseConfiguration ListStreamsCompletedServiceDoc(HttpEntity entity, Message message)
        {
            Debug.Assert(message.GetType() == typeof(ClientMessage.ListStreamsCompleted));

            var completed = message as ClientMessage.ListStreamsCompleted;
            return (completed != null && completed.Success) 
                ? Ok(entity, message) 
                : InternalServerEror(entity, message);
        }
    }
}
