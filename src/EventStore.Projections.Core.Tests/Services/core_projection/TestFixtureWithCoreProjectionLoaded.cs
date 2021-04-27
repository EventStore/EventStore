namespace EventStore.Projections.Core.Tests.Services.core_projection {
	public abstract class TestFixtureWithCoreProjectionLoaded<TLogFormat, TStreamId> : TestFixtureWithCoreProjection<TLogFormat, TStreamId> {
		protected override void PreWhen() {
			_coreProjection.LoadStopped();
		}
	}
}
