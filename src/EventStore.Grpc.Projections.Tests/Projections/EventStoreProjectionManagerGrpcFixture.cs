using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Common.Options;
using EventStore.Projections.Core;

namespace EventStore.Grpc.Projections {
	public abstract class EventStoreProjectionManagerGrpcFixture : EventStoreGrpcFixture {
		public ProjectionsSubsystem Projections => Node.Subsystems.OfType<ProjectionsSubsystem>().SingleOrDefault();
		protected virtual bool RunStandardProjections => true;

		protected EventStoreProjectionManagerGrpcFixture()
			: base(builder => builder.RunProjections(ProjectionType.All)) {
		}

		public override async Task InitializeAsync() {
			var projectionsStarted = StandardProjections.Created(Projections.MasterMainBus);
			await Node.StartAsync(true);

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
