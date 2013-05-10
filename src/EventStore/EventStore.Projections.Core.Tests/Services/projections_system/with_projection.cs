using System;
using System.Collections.Generic;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Helper;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Tests.Services.projections_manager;

namespace EventStore.Projections.Core.Tests.Services.projections_system
{
    abstract class with_projection : with_projections_subsystem
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

        protected override IEnumerable<WhenStep> PreWhen()
        {
            yield return base.PreWhen().ToSteps();
            yield return
                (new ProjectionManagementMessage.Post(
                    new PublishEnvelope(_bus), _projectionMode, _projectionName,
                    "native:" + _fakeProjectionType.AssemblyQualifiedName, _projectionSource,
                    enabled: _projectionEnabled, checkpointsEnabled: _checkpointsEnabled, emitEnabled: _emitEnabled));
        }
    }
}