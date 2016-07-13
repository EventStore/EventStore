namespace EventStore.Projections.Core.Tests.Services.projections_system
{
    public abstract class with_projection_config : with_projections_subsystem
    {
        protected string _projectionName;
        protected string _projectionSource;
        protected bool _checkpointsEnabled;
        protected bool _trackEmittedStreams;
        protected bool _emitEnabled;
        
        protected override void Given()
        {
            base.Given();

            _projectionName = "test-projection";
            _projectionSource = @"";
            _checkpointsEnabled = true;
            _trackEmittedStreams = true;
            _emitEnabled = true;

            NoStream("$projections-" + _projectionName + "-checkpoint");
            NoStream("$projections-" + _projectionName + "-order");
            NoStream("$projections-" + _projectionName + "-emittedstreams");
            AllWritesSucceed();
        }

    }
}
