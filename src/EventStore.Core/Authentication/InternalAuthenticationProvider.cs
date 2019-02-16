using System;
using System.Security.Principal;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Core.Authentication {
	public class InternalAuthenticationProvider : IAuthenticationProvider,
		IHandle<InternalAuthenticationProviderMessages.ResetPasswordCache> {
		private readonly IODispatcher _ioDispatcher;
		private readonly PasswordHashAlgorithm _passwordHashAlgorithm;
		private readonly LRUCache<string, Tuple<string, IPrincipal>> _userPasswordsCache;

		public InternalAuthenticationProvider(IODispatcher ioDispatcher, PasswordHashAlgorithm passwordHashAlgorithm,
			int cacheSize) {
			_ioDispatcher = ioDispatcher;
			_passwordHashAlgorithm = passwordHashAlgorithm;
			_userPasswordsCache = new LRUCache<string, Tuple<string, IPrincipal>>(cacheSize);
		}

		public void Authenticate(AuthenticationRequest authenticationRequest) {
			Tuple<string, IPrincipal> cached;
			if (_userPasswordsCache.TryGet(authenticationRequest.Name, out cached)) {
				AuthenticateWithPassword(authenticationRequest, cached.Item1, cached.Item2);
			} else {
				var userStreamId = "$user-" + authenticationRequest.Name;
				_ioDispatcher.ReadBackward(userStreamId, -1, 1, false, SystemAccount.Principal,
					m => ReadUserDataCompleted(m, authenticationRequest));
			}
		}

		private void ReadUserDataCompleted(ClientMessage.ReadStreamEventsBackwardCompleted completed,
			AuthenticationRequest authenticationRequest) {
			try {
				if (completed.Result == ReadStreamResult.StreamDeleted ||
				    completed.Result == ReadStreamResult.NoStream ||
				    completed.Result == ReadStreamResult.AccessDenied) {
					authenticationRequest.Unauthorized();
					return;
				}

				if (completed.Result == ReadStreamResult.Error) {
					authenticationRequest.NotReady();
					return;
				}

				var userData = completed.Events[0].Event.Data.ParseJson<UserData>();
				if (userData.LoginName != authenticationRequest.Name) {
					authenticationRequest.Error();
					return;
				}

				if (userData.Disabled)
					authenticationRequest.Unauthorized();
				else
					AuthenticateWithPasswordHash(authenticationRequest, userData);
			} catch {
				authenticationRequest.Unauthorized();
			}
		}

		private void AuthenticateWithPasswordHash(AuthenticationRequest authenticationRequest, UserData userData) {
			if (!_passwordHashAlgorithm.Verify(authenticationRequest.SuppliedPassword, userData.Hash, userData.Salt)) {
				authenticationRequest.Unauthorized();
				return;
			}

			var principal = CreatePrincipal(userData);
			CachePassword(authenticationRequest.Name, authenticationRequest.SuppliedPassword, principal);
			authenticationRequest.Authenticated(principal);
		}

		private static OpenGenericPrincipal CreatePrincipal(UserData userData) {
			var roles = new string[userData.Groups != null ? userData.Groups.Length + 1 : 1];
			if (userData.Groups != null)
				Array.Copy(userData.Groups, roles, userData.Groups.Length);
			roles[roles.Length - 1] = userData.LoginName;
			var principal = new OpenGenericPrincipal(new GenericIdentity(userData.LoginName), roles);
			return principal;
		}

		private void CachePassword(string loginName, string password, IPrincipal principal) {
			_userPasswordsCache.Put(loginName, Tuple.Create(password, principal));
		}

		private void AuthenticateWithPassword(AuthenticationRequest authenticationRequest, string correctPassword,
			IPrincipal principal) {
			if (authenticationRequest.SuppliedPassword != correctPassword) {
				authenticationRequest.Unauthorized();
				return;
			}

			authenticationRequest.Authenticated(principal);
		}

		public void Handle(InternalAuthenticationProviderMessages.ResetPasswordCache message) {
			_userPasswordsCache.Remove(message.LoginName);
		}
	}
}
