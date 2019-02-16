using System;
using System.Security.Principal;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.ClientAPI.Embedded {
	internal static class AuthenticationExtensions {
		public static void PublishWithAuthentication(
			this IPublisher publisher, IAuthenticationProvider authenticationProvider, UserCredentials userCredentials,
			Action<Exception> setException, Func<IPrincipal, Message> onUser) {
			if (userCredentials == null) {
				var message = onUser(null);

				publisher.Publish(message);

				return;
			}

			authenticationProvider.Authenticate(new EmbeddedAuthenticationRequest(userCredentials.Username,
				userCredentials.Password, setException, user => {
					var message = onUser(user);

					publisher.Publish(message);
				}));
		}
	}
}
