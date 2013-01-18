using System.Net;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Checkpoints
{
    [TestFixture]
    public class when_creating_file_multi_checkpoint_from_existing_file : SpecificationWithFile
    {
        private static readonly IPEndPoint EndPoint1 = new IPEndPoint(IPAddress.Loopback, 1000);
        private static readonly IPEndPoint EndPoint2 = new IPEndPoint(IPAddress.Loopback, 2000);
        private static readonly IPEndPoint EndPoint3 = new IPEndPoint(IPAddress.Loopback, 3000);

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            var checkpoint = new FileMultiCheckpoint(Filename, 3);
            checkpoint.Update(EndPoint1, 1000);
            checkpoint.Update(EndPoint2, 2000);
            checkpoint.Update(EndPoint3, 3000);
            checkpoint.Flush();
            checkpoint.Dispose();
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test]
        public void the_checkpoint_contains_all_previously_stored_data()
        {
            using (var checkpoint = new FileMultiCheckpoint(Filename, 3))
            {
                long check;
                Assert.IsTrue(checkpoint.TryGetMinMax(out check));
                Assert.AreEqual(1000, check);
            }
        }

        [Test]
        public void the_checkpoint_loads_and_processed_all_stored_data_even_if_best_count_is_smaller_now()
        {
            using (var checkpoint = new FileMultiCheckpoint(Filename, 2))
            {
                long check;
                Assert.IsTrue(checkpoint.TryGetMinMax(out check));
                Assert.AreEqual(2000, check);
            }
        }

        [Test]
        public void the_checkpoint_loads_without_errors_if_stored_data_count_is_less_than_best_count()
        {
            using (var checkpoint = new FileMultiCheckpoint(Filename, 10))
            {
                long check;
                Assert.IsTrue(checkpoint.TryGetMinMax(out check));
                Assert.AreEqual(1000, check);
            }
        }
    }
}