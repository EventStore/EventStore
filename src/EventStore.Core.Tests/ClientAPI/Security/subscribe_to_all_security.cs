﻿using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), Category("Network")]
	public class subscribe_to_all_security : AuthenticationTestBase {
		[Test]
		public async Task subscribing_to_all_with_not_existing_credentials_is_not_authenticated() {
			await AssertEx.ThrowsAsync<NotAuthenticatedException>(() => SubscribeToAll("badlogin", "badpass"));
		}

		[Test]
		public async Task subscribing_to_all_with_no_credentials_is_denied() {
			await AssertEx.ThrowsAsync<AccessDeniedException>(() => SubscribeToAll(null, null));
		}

		[Test]
		public async Task subscribing_to_all_with_not_authorized_user_credentials_is_denied() {
			await AssertEx.ThrowsAsync<AccessDeniedException>(() => SubscribeToAll("user2", "pa$$2"));
		}

		[Test]
		public async Task subscribing_to_all_with_authorized_user_credentials_succeeds() {
			await SubscribeToAll("user1", "pa$$1");
		}

		[Test]
		public async Task subscribing_to_all_with_admin_user_credentials_succeeds() {
			await SubscribeToAll("adm", "admpa$$");
		}
	}
}
