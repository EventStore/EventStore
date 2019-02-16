namespace EventStore.Projections.Core.Tests.Services.core_projection {
	public abstract class TestFixtureWithCoreProjectionLoaded : TestFixtureWithCoreProjection {
		protected override void PreWhen() {
			_coreProjection.LoadStopped();
		}
	}
}
