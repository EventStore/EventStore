using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Common.Options;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core;

namespace EventStore.Client.Projections {
	public abstract class EventStoreProjectionManagerGrpcFixture : EventStoreGrpcFixture {
		public ProjectionsSubsystem Projections => Node.Subsystems.OfType<ProjectionsSubsystem>().SingleOrDefault();
		protected virtual bool RunStandardProjections => true;

		protected EventStoreProjectionManagerGrpcFixture()
			: base(builder => builder.RunProjections(ProjectionType.All)) {
		}

		public override async Task InitializeAsync() {
			var projectionsStarted = StandardProjections.Created(Projections.LeaderMainBus);
			await Node.StartAsync(true);

			var createUser = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			var envelope  = new CallbackEnvelope(m => {
				if (m is UserManagementMessage.ResponseMessage rm) {
					if (rm.Success) createUser.TrySetResult(true);
					else createUser.TrySetException(new Exception($"Create user failed {rm.Error}"));
				} else {
					createUser.TrySetException(new Exception($"Wrong expected message type {m.GetType().FullName}"));
				}
			});
			Node.MainQueue.Publish(new UserManagementMessage.Create(envelope, SystemAccounts.System, TestCredentials.TestUser1.Username, "test", Array.Empty<string>(), TestCredentials.TestUser1.Password));
			await createUser.Task;

			await projectionsStarted.WithTimeout(TimeSpan.FromMinutes(5));

			if (RunStandardProjections) {
				await Task.WhenAll(StandardProjections.Names.Select(name =>
					Client.ProjectionsManager.EnableAsync(name, TestCredentials.Root)));
			}

			await Given().WithTimeout(TimeSpan.FromMinutes(5));
			await When().WithTimeout(TimeSpan.FromMinutes(5));
		}
	}
}
