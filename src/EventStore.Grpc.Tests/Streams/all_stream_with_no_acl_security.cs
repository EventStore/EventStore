using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.Streams {
	public class all_stream_with_no_acl_security
		: IClassFixture<all_stream_with_no_acl_security.Fixture> {
		private readonly Fixture _fixture;

		public all_stream_with_no_acl_security(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task write_to_all_is_never_allowed() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream(SecurityFixture.AllStream));
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream(SecurityFixture.AllStream, TestCredentials.TestUser1));
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.AppendStream(SecurityFixture.AllStream, TestCredentials.TestAdmin));
		}

		[Fact]
		public async Task delete_of_all_is_never_allowed() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.DeleteStream(SecurityFixture.AllStream));
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.DeleteStream(SecurityFixture.AllStream, TestCredentials.TestUser1));
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.DeleteStream(SecurityFixture.AllStream, TestCredentials.TestAdmin));
		}


		[Fact]
		public async Task reading_and_subscribing_is_not_allowed_when_no_credentials_are_passed() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadAllForward());
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadAllBackward());
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadMeta(SecurityFixture.AllStream));
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.SubscribeToAll());
		}

		[Fact]
		public async Task reading_and_subscribing_is_not_allowed_for_usual_user() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadAllForward(TestCredentials.TestUser1));
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadAllBackward(TestCredentials.TestUser1));
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.ReadMeta(SecurityFixture.AllStream, TestCredentials.TestUser1));
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.SubscribeToAll(TestCredentials.TestUser1));
		}

		[Fact]
		public async Task reading_and_subscribing_is_allowed_for_admin_user() {
			await _fixture.ReadAllForward(TestCredentials.TestAdmin);
			await _fixture.ReadAllBackward(TestCredentials.TestAdmin);
			await _fixture.ReadMeta(SecurityFixture.AllStream, TestCredentials.TestAdmin);
			await _fixture.SubscribeToAll(TestCredentials.TestAdmin);
		}

		[Fact]
		public async Task meta_write_is_not_allowed_when_no_credentials_are_passed() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.WriteMeta(SecurityFixture.AllStream));
		}

		[Fact]
		public async Task meta_write_is_not_allowed_for_usual_user() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.WriteMeta(SecurityFixture.AllStream, TestCredentials.TestUser1));
		}

		[Fact]
		public Task meta_write_is_allowed_for_admin_user() {
			return _fixture.WriteMeta(SecurityFixture.AllStream, TestCredentials.TestAdmin);
		}

		public class Fixture : SecurityFixture {
			protected override async Task Given() {
				await base.Given();
				await Client.SetStreamMetadataAsync(AllStream, AnyStreamRevision.Any, new StreamMetadata(),
					TestCredentials.Root);
			}

			protected override Task When() => Task.CompletedTask;
		}
	}
}
