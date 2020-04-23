using System;
using System.Collections.Generic;
using System.Security.Claims;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using NUnit.Framework;
using System.Linq;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.runas {
	namespace when_posting_a_persistent_projection {
		[TestFixture, Ignore("Persistent projections are admin only")]
		public class authenticated : TestFixtureWithProjectionCoreAndManagementServices {
			private string _projectionName;
			private ClaimsPrincipal _testUserPrincipal;

			private string _projectionBody = @"fromAll().when({$any:function(s,e){return s;}});";

			protected override void Given() {
				_projectionName = "test-projection";
				_projectionBody = @"fromAll().when({$any:function(s,e){return s;}});";
				_testUserPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
					new [] {
						new Claim(ClaimTypes.Name,"test-user"),
						new Claim(ClaimTypes.Role,"test-role1"), 
						new Claim(ClaimTypes.Role,"test-role2")
					}
					, "ES-Test"));

				AllWritesSucceed();
				NoOtherStreams();
			}

			protected override IEnumerable<WhenStep> When() {
				yield return new ProjectionSubsystemMessage.StartComponents(Guid.NewGuid());
				yield return
					new ProjectionManagementMessage.Command.Post(
						new PublishEnvelope(GetInputQueue()), ProjectionMode.Continuous, _projectionName,
						new ProjectionManagementMessage.RunAs(_testUserPrincipal), "JS", _projectionBody, enabled: true,
						checkpointsEnabled: true, emitEnabled: true, trackEmittedStreams: true, enableRunAs: true);
			}

			[Test, Ignore("Persistent projections are admin only")]
			public void anonymous_cannot_retrieve_projection_query() {
				GetInputQueue()
					.Publish(
						new ProjectionManagementMessage.Command.GetQuery(
							Envelope, _projectionName, ProjectionManagementMessage.RunAs.Anonymous));
				_queue.Process();

				Assert.IsTrue(HandledMessages.OfType<ProjectionManagementMessage.NotAuthorized>().Any());
			}

			[Test]
			public void projection_owner_can_retrieve_projection_query() {
				GetInputQueue()
					.Publish(
						new ProjectionManagementMessage.Command.GetQuery(
							Envelope, _projectionName, new ProjectionManagementMessage.RunAs(_testUserPrincipal)));
				_queue.Process();

				var query = HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().FirstOrDefault();
				Assert.NotNull(query);
				Assert.AreEqual(_projectionBody, query.Query);
			}
		}

		[TestFixture]
		public class anonymous : TestFixtureWithProjectionCoreAndManagementServices {
			private string _projectionName;

			private string _projectionBody = @"fromAll().when({$any:function(s,e){return s;}});";

			protected override void Given() {
				_projectionName = "test-projection";
				_projectionBody = @"fromAll().when({$any:function(s,e){return s;}});";

				AllWritesSucceed();
				NoOtherStreams();
			}

			protected override IEnumerable<WhenStep> When() {
				yield return new ProjectionSubsystemMessage.StartComponents(Guid.NewGuid());
				yield return
					new ProjectionManagementMessage.Command.Post(
						new PublishEnvelope(GetInputQueue()), ProjectionMode.Continuous, _projectionName,
						ProjectionManagementMessage.RunAs.Anonymous, "JS", _projectionBody, enabled: true,
						checkpointsEnabled: true, emitEnabled: true, trackEmittedStreams: true, enableRunAs: true);
			}

			[Test]
			public void replies_with_not_authorized() {
				Assert.IsTrue(HandledMessages.OfType<ProjectionManagementMessage.NotAuthorized>().Any());
			}
		}
	}

	namespace when_setting_new_runas_account {
		public abstract class with_runas_projection : TestFixtureWithProjectionCoreAndManagementServices {
			protected string _projectionName;
			protected ClaimsPrincipal _testUserPrincipal;
			protected ClaimsPrincipal _testUserPrincipal2;

			protected string _projectionBody = @"fromAll().when({$any:function(s,e){return s;}});";

			protected override void Given() {
				_projectionName = "test-projection";
				_projectionBody = @"fromAll().when({$any:function(s,e){return s;}});";
				_testUserPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
					new [] {
						new Claim(ClaimTypes.Name,"test-user"),
						new Claim(ClaimTypes.Role,"test-role1"), 
						new Claim(ClaimTypes.Role,"test-role2")
					}
					, "ES-Test"));
				_testUserPrincipal2 = new ClaimsPrincipal(new ClaimsIdentity(
					new [] {
						new Claim(ClaimTypes.Name,"test-user2"),
						new Claim(ClaimTypes.Role,"test-role2"), 
						new Claim(ClaimTypes.Role,"test-role3")
					}
					, "ES-Test"));

				AllWritesSucceed();
				NoOtherStreams();
			}

			protected override IEnumerable<WhenStep> PreWhen() {
				yield return new ProjectionSubsystemMessage.StartComponents(Guid.NewGuid());
				yield return
					new ProjectionManagementMessage.Command.Post(
						new PublishEnvelope(GetInputQueue()), ProjectionMode.Continuous, _projectionName,
						new ProjectionManagementMessage.RunAs(_testUserPrincipal), "JS", _projectionBody, enabled: true,
						checkpointsEnabled: true, emitEnabled: true, trackEmittedStreams: true, enableRunAs: true);
			}
		}

		[TestFixture, Ignore("Persistent projections are admin only")]
		public class as_another_user : with_runas_projection {
			protected override IEnumerable<WhenStep> When() {
				yield return
					new ProjectionManagementMessage.Command.SetRunAs(
						Envelope, _projectionName, new ProjectionManagementMessage.RunAs(_testUserPrincipal2),
						ProjectionManagementMessage.Command.SetRunAs.SetRemove.Set);
			}

			[Test]
			public void new_projection_owner_can_retrieve_projection_query() {
				GetInputQueue()
					.Publish(
						new ProjectionManagementMessage.Command.GetQuery(
							Envelope, _projectionName, new ProjectionManagementMessage.RunAs(_testUserPrincipal2)));
				_queue.Process();

				var query = HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().FirstOrDefault();
				Assert.NotNull(query);
				Assert.AreEqual(_projectionBody, query.Query);
			}
		}
	}
}
