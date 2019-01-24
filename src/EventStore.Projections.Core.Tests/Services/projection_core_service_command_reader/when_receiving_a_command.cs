using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader
{
    [TestFixture]
    public class when_receiving_a_command : specification_with_projection_core_service_command_reader_started
    {
        protected override IEnumerable<WhenStep> When()
        {
            yield return
                CreateWriteEvent(
                    "$projections-$" + _serviceId,
                    "$start",
                    "{\"id\":\"" + Guid.NewGuid().ToString("N") + "\"}",
                    null,
                    true);
        }

        [Test]
        public void it_works()
        {
            Assert.IsNotEmpty(_serviceId);
        }
    }
}
