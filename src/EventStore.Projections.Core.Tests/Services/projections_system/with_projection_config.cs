using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services.projections_system {
	public abstract class with_projection_config : with_projections_subsystem {
		protected string _projectionName;
		protected string _projectionSource;
		protected bool _checkpointsEnabled;
		protected bool _trackEmittedStreams;
		protected bool _emitEnabled;

		protected override void Given() {
			base.Given();

			_projectionName = "test-projection";
			_projectionSource = @"";
			_checkpointsEnabled = true;
			_trackEmittedStreams = true;
			_emitEnabled = true;

			NoStream(ProjectionNamesBuilder.ProjectionsStreamPrefix + _projectionName + "-checkpoint");
			NoStream(ProjectionNamesBuilder.ProjectionsStreamPrefix + _projectionName + "-order");
			NoStream(ProjectionNamesBuilder.ProjectionsStreamPrefix + _projectionName + "-emittedstreams");
			AllWritesSucceed();
		}
	}
}
