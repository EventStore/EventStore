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
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Transport.Http;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http
{
    public class HttpResponseConfiguratorArgs
    {
        public readonly string UserHostName;
        public readonly ICodec ResponseCodec;

        public HttpResponseConfiguratorArgs(string userHostName, ICodec responseCodec)
        {
            UserHostName = userHostName;
            ResponseCodec = responseCodec;
        }

        public static implicit operator HttpResponseConfiguratorArgs(HttpEntity entity)
        {
            return new HttpResponseConfiguratorArgs(entity.UserHostName, entity.ResponseCodec);
        }
    }

    public class HttpResponseFormatterArgs
    {
        public readonly string UserHostName;
        public readonly ICodec ResponseCodec;

        public HttpResponseFormatterArgs(string userHostName, ICodec responseCodec)
        {
            UserHostName = userHostName;
            ResponseCodec = responseCodec;
        }

        public static implicit operator HttpResponseFormatterArgs(HttpEntity entity)
        {
            return new HttpResponseFormatterArgs(entity.UserHostName, entity.ResponseCodec);
        }
    }

    public class SendToHttpEnvelope : IEnvelope
    {
        private readonly IPublisher _networkSendQueue;
        private readonly HttpEntity _entity;
        private readonly Func<HttpResponseFormatterArgs, Message, string> _formatter;
        private readonly Func<HttpResponseConfiguratorArgs, Message, ResponseConfiguration> _configurator;

        public SendToHttpEnvelope(IPublisher networkSendQueue, 
                                  HttpEntity entity,
                                  Func<HttpResponseFormatterArgs, Message, string> formatter,
                                  Func<HttpResponseConfiguratorArgs, Message, ResponseConfiguration> configurator)
        {
            Ensure.NotNull(networkSendQueue, "networkSendQueue");
            Ensure.NotNull(entity, "entity");
            Ensure.NotNull(formatter, "formatter");
            Ensure.NotNull(configurator, "configurator");

            _networkSendQueue = networkSendQueue;
            _entity = entity;
            _formatter = formatter;
            _configurator = configurator;
        }

        public void ReplyWith<T>(T message) where T : Message
        {
            Ensure.NotNull(message, "message");
            var responseConfiguration = _configurator(_entity, message);
            var data = _formatter(_entity, message);
            _networkSendQueue.Publish(new HttpMessage.HttpSend(_entity.Manager, responseConfiguration, data, message));
        }
    }

    public class SendToHttpEnvelope<TExpectedResponseMessage> : IEnvelope where TExpectedResponseMessage : Message
    {
        private readonly Func<ICodec, TExpectedResponseMessage, string> _formatter;
        private readonly Func<ICodec, TExpectedResponseMessage, ResponseConfiguration> _configurator;
        private readonly IEnvelope _notMatchingEnvelope;

        private readonly IEnvelope _httpEnvelope;

        public SendToHttpEnvelope(IPublisher networkSendQueue, 
                                  HttpEntity entity, 
                                  Func<ICodec, TExpectedResponseMessage, string> formatter, 
                                  Func<ICodec, TExpectedResponseMessage, ResponseConfiguration> configurator,
                                  IEnvelope notMatchingEnvelope)
        {
            _formatter = formatter;
            _configurator = configurator;
            _notMatchingEnvelope = notMatchingEnvelope;
            _httpEnvelope = new SendToHttpEnvelope(networkSendQueue, entity, Formatter, Configurator);
        }

        private ResponseConfiguration Configurator(HttpResponseConfiguratorArgs http, Message message)
        {
            try
            {
                return _configurator(http.ResponseCodec, (TExpectedResponseMessage)message);
            }
            catch (InvalidCastException)
            {
                //NOTE: using exceptions to allow handling errors in debugger
                return new ResponseConfiguration(500, "Internal server error", "text/plain", Encoding.UTF8);
            }
        }

        private string Formatter(HttpResponseFormatterArgs http, Message message)
        {
            try
            {
                return _formatter(http.ResponseCodec, (TExpectedResponseMessage)message);
            }
            catch (InvalidCastException)
            {
                //NOTE: using exceptions to allow handling errors in debugger
                return "";
            }
        }  

        public void ReplyWith<T>(T message) where T : Message
        {
            if (message is TExpectedResponseMessage || _notMatchingEnvelope == null)
                _httpEnvelope.ReplyWith(message);
            else
                _notMatchingEnvelope.ReplyWith(message);
        }
    }
}