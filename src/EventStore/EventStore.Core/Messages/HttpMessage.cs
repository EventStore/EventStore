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
        public class HttpSend: Message
        {
            public readonly HttpEntity Entity;
            public readonly Func<HttpEntity, Message, string> Formatter;
            public readonly Func<HttpEntity, Message, ResponseConfiguration> Configurator;
            public readonly Message Message;

            public HttpSend(HttpEntity entity, 
                            Func<HttpEntity, Message, string> formatter, 
                            Func<HttpEntity, Message, ResponseConfiguration> configurator,
                            Message message)
            {
                Entity = entity;
                Formatter = formatter;
                Configurator = configurator;
                Message = message;
            }
        }

        public class DeniedToHandle : Message
        {
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

        public class UpdatePendingRequests : Message
        {
            public readonly ServiceAccessibility Accessibility;

            public UpdatePendingRequests(ServiceAccessibility accessibility)
            {
                Accessibility = accessibility;
            }
        }

        public class TextMessage : Message
        {
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