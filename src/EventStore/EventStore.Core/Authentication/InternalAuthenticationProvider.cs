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

using System;
using System.Security.Principal;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Core.Authentication
{
	public class InternalAuthenticationProvider : IAuthenticationProvider, IHandle<InternalAuthenticationProviderMessages.ResetPasswordCache>
	{
        private readonly IODispatcher _ioDispatcher;
        private readonly PasswordHashAlgorithm _passwordHashAlgorithm;
        private readonly LRUCache<string, CachedPrincipal> _userPasswordsCache;

        public InternalAuthenticationProvider(IODispatcher ioDispatcher, PasswordHashAlgorithm passwordHashAlgorithm, int cacheSize)
        {
            _ioDispatcher = ioDispatcher;
            _passwordHashAlgorithm = passwordHashAlgorithm;
            _userPasswordsCache = new LRUCache<string, CachedPrincipal>(cacheSize);
        }

        public void Authenticate(AuthenticationRequest authenticationRequest)
        {
	        CachedPrincipal cached;
            if (_userPasswordsCache.TryGet(authenticationRequest.Name, out cached))
            {
                AuthenticateCachedPrincipal(authenticationRequest, cached);
            }
            else
            {
                var userStreamId = "$user-" + authenticationRequest.Name;
                _ioDispatcher.ReadBackward(userStreamId, -1, 1, false, SystemAccount.Principal, 
                                           m => ReadUserDataCompleted(m, authenticationRequest));
            }
        }

        private void ReadUserDataCompleted(ClientMessage.ReadStreamEventsBackwardCompleted completed, 
                                           AuthenticationRequest authenticationRequest)
        {
            try
            {
                if (completed.Result != ReadStreamResult.Success)
                {
                    authenticationRequest.Unauthorized();
                    return;
                }
                var userData = completed.Events[0].Event.Data.ParseJson<UserData>();
                if (userData.LoginName != authenticationRequest.Name)
                {
                    authenticationRequest.Error();
                    return;
                }
                if (userData.Disabled)
                    authenticationRequest.Unauthorized();
                else
                    AuthenticateWithPasswordHash(authenticationRequest, userData);
            }
            catch
            {
                authenticationRequest.Unauthorized();
            }
        }

        private void AuthenticateWithPasswordHash(AuthenticationRequest authenticationRequest, UserData userData)
        {
            if (!_passwordHashAlgorithm.Verify(authenticationRequest.SuppliedPassword, userData.Hash, userData.Salt))
            {
                authenticationRequest.Unauthorized();
                return;
            }
            var principal = CreatePrincipal(userData);

			CachePassword(authenticationRequest.Name, userData.Hash, userData.Salt, principal);
            authenticationRequest.Authenticated(principal);
        }

        private static OpenGenericPrincipal CreatePrincipal(UserData userData)
        {
            var roles = new string[userData.Groups != null ? userData.Groups.Length + 1 : 1];
            if (userData.Groups != null)
                Array.Copy(userData.Groups, roles, userData.Groups.Length);
            roles[roles.Length - 1] = userData.LoginName;
            var principal = new OpenGenericPrincipal(new GenericIdentity(userData.LoginName), roles);
            return principal;
        }

        private void CachePassword(string loginName, string hashedPassword, string salt, IPrincipal principal)
        {
	        var cachedPrincipal = new CachedPrincipal(hashedPassword, salt, principal);
	        _userPasswordsCache.Put(loginName, cachedPrincipal);
        }

        private void AuthenticateCachedPrincipal(AuthenticationRequest authenticationRequest, CachedPrincipal cached)
        {
			if (!_passwordHashAlgorithm.Verify(authenticationRequest.SuppliedPassword, cached.HashedPassword, cached.PasswordSalt))
			{
				authenticationRequest.Unauthorized();
				_userPasswordsCache.Remove(authenticationRequest.Name);
				return;
			}

            authenticationRequest.Authenticated(cached.Principal);
        }

        public void Handle(InternalAuthenticationProviderMessages.ResetPasswordCache message)
        {
            _userPasswordsCache.Remove(message.LoginName);
        }
    }
}
