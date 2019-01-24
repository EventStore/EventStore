using System.Collections.Generic;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;
using System;
using EventStore.Projections.Core.Services.Processing;
using System.Linq;
using EventStore.Core.Messages;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader
{
    [TestFixture]
    public class when_starting : specification_with_projection_core_service_command_reader
    {
        protected override IEnumerable<WhenStep> When()
        {
            var uniqueId = Guid.NewGuid();
            var startCore = new ProjectionCoreServiceMessage.StartCore(uniqueId);
            var startReader = CreateWriteEvent(ProjectionNamesBuilder.BuildControlStreamName(uniqueId), "$response-reader-started", "{}");
            yield return new WhenStep(startCore, startReader);
        }

        [Test]
        public void registers_core_service()
        {
            AssertLastEventIs("$projections-$master", "$projection-worker-started");
            AssertLastEventJson("$projections-$master", new {Id___exists = ""});
        }
    }
}
