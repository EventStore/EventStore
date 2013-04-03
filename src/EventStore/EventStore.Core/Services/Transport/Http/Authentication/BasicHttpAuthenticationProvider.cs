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

using System.Net;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.Core.Services.Transport.Http.Authentication
{
    class BasicHttpAuthenticationProvider : AuthenticationProvider
    {
        private readonly IODispatcher _ioDispatcher;

        public BasicHttpAuthenticationProvider(IODispatcher ioDispatcher)
        {
            _ioDispatcher = ioDispatcher;
        }

        public override bool Authenticate(IncomingHttpRequestMessage message)
        {
            var context = message.Context;
            var basicIdentity = context.User != null ? context.User.Identity as HttpListenerBasicIdentity : null;
            if (basicIdentity != null)
            {
                var userStreamId = "$user-" + basicIdentity.Name;
                _ioDispatcher.ReadBackward(userStreamId, -1, 1, false, m => ReadUserDataCompleted(message, m));
                return true;
            }
            return false;
        }

        private void ReadUserDataCompleted(
            IncomingHttpRequestMessage message, ClientMessage.ReadStreamEventsBackwardCompleted completed)
        {
            var context = message.Context;
            if (completed.Result != ReadStreamResult.Success)
            {
                ReplyUnauthorized(context);
                return;
            }
            var basicIdentity = (HttpListenerBasicIdentity) context.User.Identity;
            var userData = completed.Events[0].Event.Data.Deserialize<UserData>();
            if (userData.Password != basicIdentity.Password)
            {
                ReplyUnauthorized(context);
                return;
            }
            Authenticated(message, context.User);
        }

        private class UserData
        {
            public string Password { get; set; }
        }
    }
}
