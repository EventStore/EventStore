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
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Core.Util;

namespace EventStore.Core.Services.Transport.Http.Authentication
{
    public class BasicHttpAuthenticationProvider : AuthenticationProvider
    {
        private readonly IODispatcher _ioDispatcher;
        private readonly PasswordHashAlgorithm _passwordHashAlgorithm;
        private readonly LRUCache<string, string> _userPasswordsCache;

        public BasicHttpAuthenticationProvider(
            IODispatcher ioDispatcher, PasswordHashAlgorithm passwordHashAlgorithm, int cacheSize = 1000)
        {
            _ioDispatcher = ioDispatcher;
            _passwordHashAlgorithm = passwordHashAlgorithm;
            _userPasswordsCache = new LRUCache<string, string>(cacheSize);
        }

        public override bool Authenticate(IncomingHttpRequestMessage message)
        {
            //NOTE: this method can be invoked on multiple threads - needs to be thread safe
            var entity = message.Entity;
            var basicIdentity = entity.User != null ? entity.User.Identity as HttpListenerBasicIdentity : null;
            if (basicIdentity != null)
            {
                HttpBasicAuthenticate(message, basicIdentity);
                return true;
            }
            return false;
        }

        private void HttpBasicAuthenticate(IncomingHttpRequestMessage message, HttpListenerBasicIdentity basicIdentity)
        {
            string correctPassword;
            if (_userPasswordsCache.TryGet(basicIdentity.Name, out correctPassword))
            {
                AuthenticateWithPassword(message, correctPassword, basicIdentity.Password);
            }
            else
            {
                var userStreamId = "$user-" + basicIdentity.Name;
                _ioDispatcher.ReadBackward(userStreamId, -1, 1, false, m => ReadUserDataCompleted(message, m));
            }
        }

        private void ReadUserDataCompleted(
            IncomingHttpRequestMessage message, ClientMessage.ReadStreamEventsBackwardCompleted completed)
        {
            try
            {
                var entity = message.Entity;
                if (completed.Result != ReadStreamResult.Success)
                {
                    ReplyUnauthorized(entity);
                    return;
                }
                var basicIdentity = (HttpListenerBasicIdentity) entity.User.Identity;
                var userData = completed.Events[0].Event.Data.ParseJson<UserData>();

                AuthenticateWithPasswordHash(message, userData, basicIdentity);
            }
            catch
            {
                ReplyUnauthorized(message.Entity);
            }
        }

        private void AuthenticateWithPasswordHash(
            IncomingHttpRequestMessage message, UserData userData, HttpListenerBasicIdentity basicHttpIdentity)
        {
            var entity = message.Entity;
            
            if (!_passwordHashAlgorithm.Verify(basicHttpIdentity.Password, userData.Hash, userData.Salt))
            {
                ReplyUnauthorized(entity);
                return;
            }
            CachePassword(basicHttpIdentity.Name, basicHttpIdentity.Password);
            Authenticated(message, entity.User);
        }

        private void CachePassword(string loginName, string password)
        {
            _userPasswordsCache.Put(loginName, password);
        }

        private void AuthenticateWithPassword(
            IncomingHttpRequestMessage message, string correctPassword, string suppliedPassword)
        {
            var entity = message.Entity;

            if (suppliedPassword != correctPassword)
            {
                ReplyUnauthorized(entity);
            }

            Authenticated(message, entity.User);
        }

    }
}
