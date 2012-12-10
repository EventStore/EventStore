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
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Transport.Http;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services
{
    public class NetworkSendService : IPublisher
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<NetworkSendService>();

        private readonly MultiQueuedHandler _tcpMultiHandler;
        private readonly MultiQueuedHandler _httpMultiHandler;

        public NetworkSendService(int tcpQueueCount, int httpQueueCount)
        {
            _tcpMultiHandler = new MultiQueuedHandler(
                tcpQueueCount,
                queueNum => new QueuedHandler(new NarrowingHandler<Message, TcpMessage.TcpSend>(new TcpSendSubservice()),
                                              string.Format("Outgoing TCP #{0}", queueNum + 1),
                                              watchSlowMsg: true,
                                              slowMsgThreshold: TimeSpan.FromMilliseconds(50)));

            _httpMultiHandler = new MultiQueuedHandler(
                httpQueueCount,
                queueNum => 
                {
                    var subservice = new HttpSendSubservice();

                    //TODO: make it faster? can we have just dictionary map inmemory bus?
                    var bus = new InMemoryBus(string.Format("Outgoing HTTP #{0} Bus", queueNum + 1), watchSlowMsg: false);
                    bus.Subscribe<HttpMessage.HttpSend>(subservice);
                    bus.Subscribe<HttpMessage.HttpSendPart>(subservice);
                    bus.Subscribe<HttpMessage.HttpBeginSend>(subservice);
                    bus.Subscribe<HttpMessage.HttpEndSend>(subservice);

                    return new QueuedHandler(new NarrowingHandler<Message, HttpMessage.HttpSend>(new HttpSendSubservice()),
                                             string.Format("Outgoing HTTP #{0}", queueNum + 1),
                                             watchSlowMsg: true,
                                             slowMsgThreshold: TimeSpan.FromMilliseconds(50));
                },
                msg =>
                {
                    //NOTE: subsequent messages to the same entity must be handled in order
                    return ((HttpMessage.HttpSendMessage) msg).HttpEntityManager.GetHashCode();
                });

            _tcpMultiHandler.Start();
            _httpMultiHandler.Start();
        }

        public void Publish(Message message)
        {
            if (message is TcpMessage.TcpSend)
            {
                _tcpMultiHandler.Publish(message);
                return;
            }

            var sendHttpMessage = message as HttpMessage.HttpSendMessage;
            if (sendHttpMessage != null)
            {
                _httpMultiHandler.Publish(sendHttpMessage);
                return;
            }
        }

        private class TcpSendSubservice: IHandle<TcpMessage.TcpSend>
        {
            public void Handle(TcpMessage.TcpSend message)
            {
                message.ConnectionManager.SendMessage(message.Message);
            }
        }

        private class HttpSendSubservice: IHandle<HttpMessage.HttpSend>,
                                          IHandle<HttpMessage.HttpBeginSend>,
                                          IHandle<HttpMessage.HttpSendPart>,
                                          IHandle<HttpMessage.HttpEndSend>
        {
            public void Handle(HttpMessage.HttpSend message)
            {
                var deniedToHandle = message.Message as HttpMessage.DeniedToHandle;
                if (deniedToHandle != null)
                {
                    int code;
                    switch (deniedToHandle.Reason)
                    {
                        case DenialReason.ServerTooBusy:
                            code = HttpStatusCode.InternalServerError;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    message.HttpEntityManager.ReplyStatus(
                        code, deniedToHandle.Details,
                        exc =>
                        Log.ErrorException(exc, "Error occurred while replying to HTTP with message {0}", message.Message));
                }
                else
                {
                    var response = message.Data;
                    var config = message.Configuration;
                    message.HttpEntityManager.ReplyTextContent(
                        response, config.Code, config.Description, config.ContentType, config.Headers,
                        exc =>
                        Log.ErrorException(exc, "Error occurred while replying to HTTP with message {0}", message.Message));
                }
            }

            public void Handle(HttpMessage.HttpBeginSend message)
            {
                var config = message.Configuration;

                message.HttpEntityManager.BeginReply(config.Code, config.Description, config.ContentType, config.Headers);
                if (message.Envelope != null)
                    message.Envelope.ReplyWith(new HttpMessage.HttpCompleted(message.CorrelationId, message.HttpEntityManager));
            }

            public void Handle(HttpMessage.HttpSendPart message)
            {
                var response = message.Data;
                message.HttpEntityManager.ContinueReplyTextContent(
                    response,
                    exc => Log.ErrorException(exc, "Error occurred while replying to HTTP with message {0}", message), 
                    () =>
                    {
                        if (message.Envelope != null)
                            message.Envelope.ReplyWith(new HttpMessage.HttpCompleted(message.CorrelationId, message.HttpEntityManager));
                    });
            }

            public void Handle(HttpMessage.HttpEndSend message)
            {
                message.HttpEntityManager.EndReply();
                if (message.Envelope != null)
                    message.Envelope.ReplyWith(new HttpMessage.HttpCompleted(message.CorrelationId, message.HttpEntityManager));
            }

        }

    }
}
