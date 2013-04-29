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
using System.Security.Principal;
using EventStore.Core.Services.Transport.Http.Messages;

namespace EventStore.Core.Services.Transport.Http.Authentication
{
    public class TrustedAuthenticationProvider : AuthenticationProvider
    {
        public override bool Authenticate(IncomingHttpRequestMessage message)
        {
            var header = message.Entity.Request.Headers["X-ES-TrustedAuth"];
            if (!string.IsNullOrEmpty(header))
            {
                var principal = CreatePrincipal(header);
                if (principal != null)
                    Authenticated(message, principal);
                else
                    ReplyUnauthorized(message.Entity);
                return true;
            }
            return false;
        }

        private IPrincipal CreatePrincipal(string header)
        {
            var loginAndGroups = header.Split(';');
            if (loginAndGroups.Length == 0 || loginAndGroups.Length > 2)
                return null;
            var login = loginAndGroups[0];
            if (loginAndGroups.Length == 2)
            {
                var groups = loginAndGroups[1];
                var groupsSplit = groups.Split(',');
                var roles = new string[groupsSplit.Length + 1];
                Array.Copy(groupsSplit, roles, groupsSplit.Length);
                roles[roles.Length - 1] = login;
                for (var i = 0; i < roles.Length; i++)
                    roles[i] = roles[i].Trim();
                return new GenericPrincipal(new GenericIdentity(login), roles);
            }
            else
            {
                return new GenericPrincipal(new GenericIdentity(login), new[] {login});
            }
        }
    }
}
