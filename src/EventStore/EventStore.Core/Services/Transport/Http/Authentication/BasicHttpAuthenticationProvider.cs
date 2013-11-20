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

using System.Net;
using System.Security.Principal;
using EventStore.Core.Authentication;
using EventStore.Core.Services.Transport.Http.Messages;

namespace EventStore.Core.Services.Transport.Http.Authentication
{
    public class BasicHttpAuthenticationProvider : HttpAuthenticationProvider
    {
        private readonly IAuthenticationProvider _internalAuthenticationProvider;

        public BasicHttpAuthenticationProvider(IAuthenticationProvider internalAuthenticationProvider)
        {
            _internalAuthenticationProvider = internalAuthenticationProvider;
        }

        public override bool Authenticate(IncomingHttpRequestMessage message)
        {
            //NOTE: this method can be invoked on multiple threads - needs to be thread safe
            var entity = message.Entity;
            var basicIdentity = entity.User != null ? entity.User.Identity as HttpListenerBasicIdentity : null;
            if (basicIdentity != null)
            {
                string name = basicIdentity.Name;
                string suppliedPassword = basicIdentity.Password;

                var authenticationRequest = new HttpBasicAuthenticationRequest(this, message, name, suppliedPassword);
                _internalAuthenticationProvider.Authenticate(authenticationRequest);
                return true;
            }
            return false;
        }

        private class HttpBasicAuthenticationRequest : AuthenticationRequest
        {
            private readonly BasicHttpAuthenticationProvider _basicHttpAuthenticationProvider;
            private readonly IncomingHttpRequestMessage _message;

            public HttpBasicAuthenticationRequest(
                BasicHttpAuthenticationProvider basicHttpAuthenticationProvider, IncomingHttpRequestMessage message,
                string name, string suppliedPassword)
                : base(name, suppliedPassword)
            {
                _basicHttpAuthenticationProvider = basicHttpAuthenticationProvider;
                _message = message;
            }

            public override void Unauthorized()
            {
                ReplyUnauthorized(_message.Entity);
            }

            public override void Authenticated(IPrincipal principal)
            {
                _basicHttpAuthenticationProvider.Authenticated(_message, principal);
            }

            public override void Error()
            {
                ReplyInternalServerError(_message.Entity);
            }
        }
    }
}
