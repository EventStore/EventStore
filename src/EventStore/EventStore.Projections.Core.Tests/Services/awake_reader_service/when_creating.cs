using EventStore.Projections.Core.Services.AwakeReaderService;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.awake_reader_service
{
    [TestFixture]
    public class when_creating
    {
        [Test]
        public void it_can_ce_created()
        {
            var it = new AwakeReaderService();
        }

    }
}