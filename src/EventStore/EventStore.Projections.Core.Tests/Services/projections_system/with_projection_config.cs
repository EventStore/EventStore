using System;
using System.Collections.Generic;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Tests.Services.projections_manager;

namespace EventStore.Projections.Core.Tests.Services.projections_system
{
    public abstract class with_projection_config : with_projections_subsystem
    {
        protected string _projectionName;
        protected string _projectionSource;
        protected Type _fakeProjectionType;
        protected ProjectionMode _projectionMode;
        protected bool _checkpointsEnabled;
        protected bool _emitEnabled;
        protected bool _projectionEnabled;

        protected override void Given()
        {
            base.Given();

            _projectionName = "test-projection";
            _projectionSource = @"";
            _fakeProjectionType = typeof (FakeProjection);
            _projectionMode = ProjectionMode.Continuous;
            _checkpointsEnabled = true;
            _emitEnabled = true;
            _projectionEnabled = true;

            NoStream("$projections-" + _projectionName + "-checkpoint");
            NoStream("$projections-" + _projectionName + "-order");
            AllWritesSucceed();
        }

    }
}
