using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Services;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security {
	[TestFixture, Category("ClientAPI"), Category("LongRunning"), Category("Network")]
	public class delete_stream_security : AuthenticationTestBase {
		[Test]
		public void delete_of_all_is_never_allowed() {
			Expect<AccessDeniedException>(() => DeleteStream("$all", null, null));
			Expect<AccessDeniedException>(() => DeleteStream("$all", "user1", "pa$$1"));
			Expect<AccessDeniedException>(() => DeleteStream("$all", "adm", "admpa$$"));
		}

		[Test]
		public void deleting_normal_no_acl_stream_with_no_user_is_allowed() {
			var streamId = CreateStreamWithMeta(StreamMetadata.Build());
			ExpectNoException(() => DeleteStream(streamId, null, null));
		}

		[Test]
		public void deleting_normal_no_acl_stream_with_existing_user_is_allowed() {
			var streamId = CreateStreamWithMeta(StreamMetadata.Build());
			ExpectNoException(() => DeleteStream(streamId, "user1", "pa$$1"));
		}

		[Test]
		public void deleting_normal_no_acl_stream_with_admin_user_is_allowed() {
			var streamId = CreateStreamWithMeta(StreamMetadata.Build());
			ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
		}


		[Test]
		public void deleting_normal_user_stream_with_no_user_is_not_allowed() {
			var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole("user1"));
			Expect<AccessDeniedException>(() => DeleteStream(streamId, null, null));
		}

		[Test]
		public void deleting_normal_user_stream_with_not_authorized_user_is_not_allowed() {
			var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole("user1"));
			Expect<AccessDeniedException>(() => DeleteStream(streamId, "user2", "pa$$2"));
		}

		[Test]
		public void deleting_normal_user_stream_with_authorized_user_is_allowed() {
			var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole("user1"));
			ExpectNoException(() => DeleteStream(streamId, "user1", "pa$$1"));
		}

		[Test]
		public void deleting_normal_user_stream_with_admin_user_is_allowed() {
			var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole("user1"));
			ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
		}


		[Test]
		public void deleting_normal_admin_stream_with_no_user_is_not_allowed() {
			var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole(SystemRoles.Admins));
			Expect<AccessDeniedException>(() => DeleteStream(streamId, null, null));
		}

		[Test]
		public void deleting_normal_admin_stream_with_existing_user_is_not_allowed() {
			var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole(SystemRoles.Admins));
			Expect<AccessDeniedException>(() => DeleteStream(streamId, "user1", "pa$$1"));
		}

		[Test]
		public void deleting_normal_admin_stream_with_admin_user_is_allowed() {
			var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole(SystemRoles.Admins));
			ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
		}


		[Test]
		public void deleting_normal_all_stream_with_no_user_is_allowed() {
			var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole(SystemRoles.All));
			ExpectNoException(() => DeleteStream(streamId, null, null));
		}

		[Test]
		public void deleting_normal_all_stream_with_existing_user_is_allowed() {
			var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole(SystemRoles.All));
			ExpectNoException(() => DeleteStream(streamId, "user1", "pa$$1"));
		}

		[Test]
		public void deleting_normal_all_stream_with_admin_user_is_allowed() {
			var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole(SystemRoles.All));
			ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
		}

		// $-stream

		[Test]
		public void deleting_system_no_acl_stream_with_no_user_is_not_allowed() {
			var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build());
			Expect<AccessDeniedException>(() => DeleteStream(streamId, null, null));
		}

		[Test]
		public void deleting_system_no_acl_stream_with_existing_user_is_not_allowed() {
			var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build());
			Expect<AccessDeniedException>(() => DeleteStream(streamId, "user1", "pa$$1"));
		}

		[Test]
		public void deleting_system_no_acl_stream_with_admin_user_is_allowed() {
			var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build());
			ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
		}


		[Test]
		public void deleting_system_user_stream_with_no_user_is_not_allowed() {
			var streamId = CreateStreamWithMeta(streamPrefix: "$",
				metadata: StreamMetadata.Build().SetDeleteRole("user1"));
			Expect<AccessDeniedException>(() => DeleteStream(streamId, null, null));
		}

		[Test]
		public void deleting_system_user_stream_with_not_authorized_user_is_not_allowed() {
			var streamId = CreateStreamWithMeta(streamPrefix: "$",
				metadata: StreamMetadata.Build().SetDeleteRole("user1"));
			Expect<AccessDeniedException>(() => DeleteStream(streamId, "user2", "pa$$2"));
		}

		[Test]
		public void deleting_system_user_stream_with_authorized_user_is_allowed() {
			var streamId = CreateStreamWithMeta(streamPrefix: "$",
				metadata: StreamMetadata.Build().SetDeleteRole("user1"));
			ExpectNoException(() => DeleteStream(streamId, "user1", "pa$$1"));
		}

		[Test]
		public void deleting_system_user_stream_with_admin_user_is_allowed() {
			var streamId = CreateStreamWithMeta(streamPrefix: "$",
				metadata: StreamMetadata.Build().SetDeleteRole("user1"));
			ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
		}


		[Test]
		public void deleting_system_admin_stream_with_no_user_is_not_allowed() {
			var streamId = CreateStreamWithMeta(streamPrefix: "$",
				metadata: StreamMetadata.Build().SetDeleteRole(SystemRoles.Admins));
			Expect<AccessDeniedException>(() => DeleteStream(streamId, null, null));
		}

		[Test]
		public void deleting_system_admin_stream_with_existing_user_is_not_allowed() {
			var streamId = CreateStreamWithMeta(streamPrefix: "$",
				metadata: StreamMetadata.Build().SetDeleteRole(SystemRoles.Admins));
			Expect<AccessDeniedException>(() => DeleteStream(streamId, "user1", "pa$$1"));
		}

		[Test]
		public void deleting_system_admin_stream_with_admin_user_is_allowed() {
			var streamId = CreateStreamWithMeta(streamPrefix: "$",
				metadata: StreamMetadata.Build().SetDeleteRole(SystemRoles.Admins));
			ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
		}


		[Test]
		public void deleting_system_all_stream_with_no_user_is_allowed() {
			var streamId = CreateStreamWithMeta(streamPrefix: "$",
				metadata: StreamMetadata.Build().SetDeleteRole(SystemRoles.All));
			ExpectNoException(() => DeleteStream(streamId, null, null));
		}

		[Test]
		public void deleting_system_all_stream_with_existing_user_is_allowed() {
			var streamId = CreateStreamWithMeta(streamPrefix: "$",
				metadata: StreamMetadata.Build().SetDeleteRole(SystemRoles.All));
			ExpectNoException(() => DeleteStream(streamId, "user1", "pa$$1"));
		}

		[Test]
		public void deleting_system_all_stream_with_admin_user_is_allowed() {
			var streamId = CreateStreamWithMeta(streamPrefix: "$",
				metadata: StreamMetadata.Build().SetDeleteRole(SystemRoles.All));
			ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
		}
	}
}
