using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Common;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    [TestFixture]
    public class when_posting_an_onetime_projection_with_checkpoints_enabled : TestFixtureWithProjectionCoreAndManagementServices
    {
        private string _projectionName;
        protected override void Given()
        {
            _projectionName = "test-projection";
            AllWritesSucceed();
            NoOtherStreams();
        }

        protected override IEnumerable<WhenStep> When()
        {
            yield return new SystemMessage.BecomeMaster(Guid.NewGuid());
            yield return new SystemMessage.SystemCoreReady();
            yield return
                new ProjectionManagementMessage.Command.Post(
                    new PublishEnvelope(_bus), ProjectionMode.OneTime, _projectionName,
                    ProjectionManagementMessage.RunAs.System, "JS", @"fromAll().when({$any:function(s,e){return s;}});",
                    enabled: true, checkpointsEnabled: true, emitEnabled: false, trackEmittedStreams: false);
        }

        [Test, Category("v8")]
        public void it_should_fail_the_operation()
        {
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.OperationFailed>().Count());
        }
    }
}
