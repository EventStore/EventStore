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
using EventStore.Common.Utils;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Messages
{
    public enum DenialReason
    {
        ServerTooBusy
    }

    public static class HttpMessage
    {
        public abstract class HttpSendMessage : Message, IQueueAffineMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public int QueueId { get { return HttpEntityManager.GetHashCode(); } }

            public readonly IEnvelope Envelope;
            public readonly Guid CorrelationId;
            public readonly HttpEntityManager HttpEntityManager;

            /// <param name="envelope">non-null envelope requests HttpCompleted messages in response</param>
            protected HttpSendMessage(Guid correlationId, IEnvelope envelope, HttpEntityManager httpEntityManager)
            {
                CorrelationId = correlationId;
                Envelope = envelope;
                HttpEntityManager = httpEntityManager;
            }
        }

        public class HttpSend : HttpSendMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string Data;
            public readonly ResponseConfiguration Configuration;
            public readonly Message Message;

            public HttpSend(
                HttpEntityManager httpEntityManager, ResponseConfiguration configuration, string data, Message message)
                : base(Guid.Empty, null, httpEntityManager)
            {
                Data = data;
                Configuration = configuration;
                Message = message;
            }
        }

        public class HttpBeginSend : HttpSendMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly ResponseConfiguration Configuration;

            public HttpBeginSend(Guid correlationId, IEnvelope envelope, 
                HttpEntityManager httpEntityManager, ResponseConfiguration configuration)
                : base(correlationId, envelope, httpEntityManager)
            {
                Configuration = configuration;
            }
        }

        public class HttpSendPart : HttpSendMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string Data;

            public HttpSendPart(Guid correlationId, IEnvelope envelope, HttpEntityManager httpEntityManager, string data)
                : base(correlationId, envelope, httpEntityManager)
            {
                Data = data;
            }
        }

        public class HttpEndSend : HttpSendMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public HttpEndSend(Guid correlationId, IEnvelope envelope, HttpEntityManager httpEntityManager)
                : base(correlationId, envelope, httpEntityManager)
            {
            }
        }

        public class HttpCompleted : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;
            public readonly HttpEntityManager HttpEntityManager;

            public HttpCompleted(Guid correlationId, HttpEntityManager httpEntityManager)
            {
                CorrelationId = correlationId;
                HttpEntityManager = httpEntityManager;
            }
        }

        public class DeniedToHandle : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly DenialReason Reason;
            public readonly string Details;

            public DeniedToHandle(DenialReason reason, string details)
            {
                Reason = reason;
                Details = details;
            }
        }

        public class SendOverHttp : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly IPEndPoint EndPoint;
            public readonly Message Message;

            public SendOverHttp(IPEndPoint endPoint, Message message)
            {
                EndPoint = endPoint;
                Message = message;
            }
        }

        public class GossipSendFailed : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Exception Exception;
            public string Reason;
            public IPEndPoint Recipient;

            public GossipSendFailed(Exception exception, string reason, IPEndPoint recipient)
            {
                Exception = exception;
                Reason = reason;
                Recipient = recipient;
            }

            public override string ToString()
            {
                return string.Format("Reason: {0}, Recipient: {1}", Reason, Recipient);
            }
        }

        public class PurgeTimedOutRequests : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly ServiceAccessibility Accessibility;

            public PurgeTimedOutRequests(ServiceAccessibility accessibility)
            {
                Accessibility = accessibility;
            }
        }

        public class HttpForwardRequestedMessage : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly HttpEntityManager Manager;

            public HttpForwardRequestedMessage(HttpEntityManager manager)
            {
                Ensure.NotNull(manager, "manager");
                Manager = manager;
            }
        }

        public class HttpForwardMessage : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly HttpEntityManager Manager;
            public readonly Uri BaseUri;

            public HttpForwardMessage(HttpEntityManager manager, Uri baseUri)
            {
                Ensure.NotNull(manager, "manager");
                Ensure.NotNull(baseUri, "baseUri");

                Manager = manager;
                BaseUri = baseUri;
            }
        }

        public class TextMessage : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public string Text { get; set; }

            public TextMessage()
            {
            }

            public TextMessage(string text)
            {
                Text = text;
            }

            public override string ToString()
            {
                return string.Format("Text: {0}", Text);
            }
        }
    }
}