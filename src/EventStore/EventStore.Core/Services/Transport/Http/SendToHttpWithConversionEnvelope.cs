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
using EventStore.Core.Messaging;
using EventStore.Transport.Http;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http
{
    public class SendToHttpWithConversionEnvelope<TExpectedResponseMessage, TExpectedHttpFormattedResponseMessage> :
        IEnvelope
        where TExpectedResponseMessage : Message
    {
        private readonly Func<ICodec, TExpectedHttpFormattedResponseMessage, string> _formatter;
        private readonly Func<ICodec, TExpectedHttpFormattedResponseMessage, ResponseConfiguration> _configurator;
        private readonly Func<TExpectedResponseMessage, TExpectedHttpFormattedResponseMessage> _convertor;

        private readonly IEnvelope _httpEnvelope;

        public SendToHttpWithConversionEnvelope(HttpEntity entity, Func<ICodec, TExpectedHttpFormattedResponseMessage, string> formatter, Func<ICodec, TExpectedHttpFormattedResponseMessage, ResponseConfiguration> configurator, Func<TExpectedResponseMessage, TExpectedHttpFormattedResponseMessage> convertor, IEnvelope nonMatchingEnvelope = null)
        {
            _formatter = formatter;
            _configurator = configurator;
            _convertor = convertor;
            _httpEnvelope = new SendToHttpEnvelope<TExpectedResponseMessage>(entity, Formatter, Configurator, nonMatchingEnvelope);
        }

        private ResponseConfiguration Configurator(ICodec codec, TExpectedResponseMessage message)
        {
            var convertedMessage = _convertor(message);
            return _configurator(codec, convertedMessage);
        }

        private string Formatter(ICodec codec, TExpectedResponseMessage message)
        {
            var convertedMessage = _convertor(message);
            return _formatter(codec, convertedMessage);
        }

        public void ReplyWith<T>(T message) where T : Message
        {
            _httpEnvelope.ReplyWith(message);
        }

    }
}