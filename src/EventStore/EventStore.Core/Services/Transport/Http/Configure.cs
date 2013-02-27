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
using System.Globalization;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Transport.Http;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Core.Services.Transport.Http
{
    public static class Configure
    {
        private const int MaxPossibleAge = 31536000;

        public static ResponseConfiguration Ok(string contentType)
        {
            return new ResponseConfiguration(HttpStatusCode.OK, "OK", contentType, Encoding.UTF8);
        }

        public static ResponseConfiguration OkCache(string contentType, Encoding encoding, int seconds)
        {
            return new ResponseConfiguration(
                HttpStatusCode.OK,
                "OK",
                contentType,
                encoding, 
                new KeyValuePair<string, string>("Cache-Control", string.Format("max-age={0}, public", seconds)),
                new KeyValuePair<string, string>("Vary", "Accept"));
        }

        public static ResponseConfiguration OkNoCache(string contentType, Encoding encoding, params KeyValuePair<string, string>[] headers)
        {
            return OkNoCache(contentType, encoding, null, headers);
        }

        public static ResponseConfiguration OkNoCache(string contentType, Encoding encoding, string etag, params KeyValuePair<string, string>[] headers)
        {
            var headrs = new List<KeyValuePair<string, string>>(headers);
            headrs.Add(new KeyValuePair<string, string>("Cache-Control", "max-age=0, no-cache, must-revalidate"));
            headrs.Add(new KeyValuePair<string, string>("Vary", "Accept"));
            if (etag.IsNotEmptyString())
                headrs.Add(new KeyValuePair<string, string>("ETag", string.Format("\"{0}\"", etag + ";" + contentType.GetHashCode())));
            return new ResponseConfiguration(HttpStatusCode.OK, "OK", contentType, encoding, headrs);
        }

        public static ResponseConfiguration NotFound()
        {
            return new ResponseConfiguration(HttpStatusCode.NotFound, "Not Found", null, Encoding.UTF8);
        }

        public static ResponseConfiguration Gone(string description = null)
        {
            return new ResponseConfiguration(HttpStatusCode.Gone, description ?? "Deleted", null, Encoding.UTF8);
        }

        public static ResponseConfiguration NotModified()
        {
            return new ResponseConfiguration(HttpStatusCode.NotModified, "Not Modified", null, Encoding.UTF8);
        }

        public static ResponseConfiguration BadRequest(string description = null)
        {
            return new ResponseConfiguration(HttpStatusCode.BadRequest, description ?? "Bad Request", null, Encoding.UTF8);
        }

        public static ResponseConfiguration InternalServerError(string description = null)
        {
            return new ResponseConfiguration(HttpStatusCode.InternalServerError, description ?? "Internal Server Error", null, Encoding.UTF8);
        }

        public static ResponseConfiguration ReadEventCompleted(HttpResponseConfiguratorArgs entity, Message message)
        {
            // Debug.Assert(message.GetType() == typeof(ClientMessage.ReadEventCompleted));

            var completed = message as ClientMessage.ReadEventCompleted;
            if (completed == null)
                return InternalServerError();

            switch (completed.Result)
            {
                case ReadEventResult.Success:
                    return OkCache(entity.ResponseCodec.ContentType, entity.ResponseCodec.Encoding, MaxPossibleAge);
                case ReadEventResult.NotFound:
                case ReadEventResult.NoStream:
                    return NotFound();
                case ReadEventResult.StreamDeleted:
                    return Gone();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static ResponseConfiguration ReadStreamEventsBackwardCompleted(HttpResponseConfiguratorArgs entity, Message message, bool headOfStream)
        {
            // Debug.Assert(message.GetType() == typeof(ClientMessage.ReadStreamEventsBackwardCompleted));

            var msg = message as ClientMessage.ReadStreamEventsBackwardCompleted;
            if (msg == null)
                return InternalServerError();

            switch (msg.Result)
            {
                case ReadStreamResult.Success:
                {
                    if (msg.LastEventNumber >= msg.FromEventNumber && !headOfStream)
                        return OkCache(entity.ResponseCodec.ContentType, entity.ResponseCodec.Encoding, MaxPossibleAge);
                    return OkNoCache(entity.ResponseCodec.ContentType, entity.ResponseCodec.Encoding, msg.LastEventNumber.ToString(CultureInfo.InvariantCulture));
                }
                case ReadStreamResult.NoStream:
                    return NotFound();
                case ReadStreamResult.StreamDeleted:
                    return Gone();
                case ReadStreamResult.NotModified:
                    return NotModified();
                case ReadStreamResult.Error:
                    return InternalServerError(msg.Message);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static ResponseConfiguration WriteEventsCompleted(HttpResponseConfiguratorArgs entity, Message message, string eventStreamId)
        {
            // Debug.Assert(message.GetType() == typeof(ClientMessage.WriteEventsCompleted));

            var completed = message as ClientMessage.WriteEventsCompleted;
            if (completed == null)
                return InternalServerError();

            switch (completed.Result)
            {
                case OperationResult.Success:
                {
                    return new ResponseConfiguration(
                        HttpStatusCode.Created,
                        "Created",
                        null,
                        Encoding.UTF8,
                        new KeyValuePair<string, string>("Location",
                                                         HostName.Combine(entity.UserHostName,
                                                                          "/streams/{0}/{1}",
                                                                          Uri.EscapeDataString(eventStreamId),
                                                                          completed.FirstEventNumber == 0 ? 1 : completed.FirstEventNumber)));
                }
                case OperationResult.PrepareTimeout:
                case OperationResult.CommitTimeout:
                case OperationResult.ForwardTimeout:
                    return InternalServerError("Write timeout");
                case OperationResult.WrongExpectedVersion:
                    return BadRequest("Wrong expected EventNumber");
                case OperationResult.StreamDeleted:
                    return Gone("Stream deleted");
                case OperationResult.InvalidTransaction:
                    return InternalServerError("Invalid transaction");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static ResponseConfiguration GetFreshStatsCompleted(HttpResponseConfiguratorArgs entity, Message message)
        {
            // Debug.Assert(message.GetType() == typeof(MonitoringMessage.GetFreshStatsCompleted));

            var completed = message as MonitoringMessage.GetFreshStatsCompleted;
            if (completed == null)
                return InternalServerError();

            return completed.Success ? OkNoCache(entity.ResponseCodec.ContentType, Encoding.UTF8) : NotFound();
        }

        public static ResponseConfiguration CreateStreamCompleted(HttpResponseConfiguratorArgs entity, Message message, string eventStreamId)
        {
            // Debug.Assert(message.GetType() == typeof(ClientMessage.CreateStreamCompleted));

            var completed = message as ClientMessage.CreateStreamCompleted;
            if (completed == null)
                return InternalServerError();

            switch (completed.Result)
            {
                case OperationResult.Success:
                {
                    return new ResponseConfiguration(
                        HttpStatusCode.Created,
                        "Stream created",
                        null,
                        Encoding.UTF8,
                        new KeyValuePair<string, string>("Location",
                                                         HostName.Combine(entity.UserHostName,
                                                                          "/streams/{0}",
                                                                          Uri.EscapeDataString(eventStreamId))));
                }
                case OperationResult.PrepareTimeout:
                case OperationResult.CommitTimeout:
                case OperationResult.ForwardTimeout:
                    return InternalServerError("Create timeout");

                case OperationResult.WrongExpectedVersion:
                case OperationResult.StreamDeleted:
                case OperationResult.InvalidTransaction:
                    return BadRequest(string.Format("Error code : {0}. Reason : {1}", completed.Result, completed.Message));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static ResponseConfiguration DeleteStreamCompleted(HttpResponseConfiguratorArgs entity, Message message)
        {
            // Debug.Assert(message.GetType() == typeof(ClientMessage.DeleteStreamCompleted));

            var completed = message as ClientMessage.DeleteStreamCompleted;
            if (completed == null)
                return InternalServerError();

            switch (completed.Result)
            {
                case OperationResult.Success:
                    return new ResponseConfiguration(HttpStatusCode.NoContent, "Stream deleted", null, Encoding.UTF8);

                case OperationResult.PrepareTimeout:
                case OperationResult.CommitTimeout:
                case OperationResult.ForwardTimeout:
                    return InternalServerError("Delete timeout");

                case OperationResult.WrongExpectedVersion:
                case OperationResult.StreamDeleted:
                case OperationResult.InvalidTransaction:
                    return BadRequest(string.Format("Error code : {0}. Reason : {1}", completed.Result, completed.Message));

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static ResponseConfiguration ReadAllEventsBackwardCompleted(HttpResponseConfiguratorArgs entity, Message message, bool headOfTf)
        {
            // Debug.Assert(message.GetType() == typeof(ClientMessage.ReadAllEventsBackwardCompleted));

            var msg = message as ClientMessage.ReadAllEventsBackwardCompleted;
            if (msg == null)
                return InternalServerError("Failed to read all events backward.");
            if (msg.NotModified)
                return NotModified();
            if (!headOfTf && msg.Result.CurrentPos.CommitPosition <= msg.Result.TfEofPosition)
                return OkCache(entity.ResponseCodec.ContentType, entity.ResponseCodec.Encoding, MaxPossibleAge);
            return OkNoCache(entity.ResponseCodec.ContentType, entity.ResponseCodec.Encoding, msg.Result.TfEofPosition.ToString(CultureInfo.InvariantCulture));
        }

        public static ResponseConfiguration ReadAllEventsForwardCompleted(HttpResponseConfiguratorArgs entity, Message message, bool headOfTf)
        {
            // Debug.Assert(message.GetType() == typeof(ClientMessage.ReadAllEventsForwardCompleted));

            var msg = message as ClientMessage.ReadAllEventsForwardCompleted;
            if (msg == null)
                return InternalServerError("Failed to read all events forward.");
            if (msg.NotModified)
                return NotModified();
            if (!headOfTf && msg.Result.Events.Length == msg.Result.MaxCount)
                return OkCache(entity.ResponseCodec.ContentType, entity.ResponseCodec.Encoding, MaxPossibleAge);
            return OkNoCache(entity.ResponseCodec.ContentType, entity.ResponseCodec.Encoding, msg.Result.TfEofPosition.ToString(CultureInfo.InvariantCulture));
        }
    }
}
