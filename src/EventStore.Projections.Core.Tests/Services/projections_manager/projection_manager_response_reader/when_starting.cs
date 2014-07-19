using System.Collections.Generic;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.projection_manager_response_reader
{
    [TestFixture]
    public class when_starting : specification_with_projection_manager_response_reader
    {
        protected override IEnumerable<WhenStep> When()
        {
            yield return new ProjectionManagementMessage.Starting();
        }

        [Test]
        public void registers_core_service()
        {
            Assert.Pass();
        }
    }
}
