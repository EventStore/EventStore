using System.IO;
using System.Net;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Checkpoints
{
    [TestFixture]
    public class when_creating_file_multi_checkpoint_with_no_previously_existing_file: SpecificationWithFile
    {
        private static readonly IPEndPoint EndPoint1 = new IPEndPoint(IPAddress.Loopback, 1000);
        private static readonly IPEndPoint EndPoint2 = new IPEndPoint(IPAddress.Loopback, 2000);
        private static readonly IPEndPoint EndPoint3 = new IPEndPoint(IPAddress.Loopback, 3000);
        private static readonly IPEndPoint EndPoint4 = new IPEndPoint(IPAddress.Loopback, 4000);

        private FileMultiCheckpoint _checkpoint;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            Assert.IsFalse(File.Exists(Filename));
            _checkpoint = new FileMultiCheckpoint(Filename, 3);
        }

        [TearDown]
        public override void TearDown()
        {
            _checkpoint.Dispose();
            base.TearDown();
        }

        [Test]
        public void the_checkpoint_file_is_created()
        {
            Assert.IsTrue(File.Exists(Filename));
        }

        [Test]
        public void the_checkpoint_file_is_empty()
        {
            Assert.AreEqual(0, new FileInfo(Filename).Length);
        }

        [Test]
        public void should_not_return_checkpoint_when_empty()
        {
            long checkpoint;
            Assert.IsFalse(_checkpoint.TryGetMinMax(out checkpoint));
            Assert.AreEqual(0, checkpoint);
        }

        [Test]
        public void checkpoints_are_dumped_on_disk_only_after_flush()
        {
            _checkpoint.Update(EndPoint1, 1000);

            Assert.AreEqual(0, new FileInfo(Filename).Length);

            _checkpoint.Flush();

            Assert.AreEqual(4 + 4 + 8, new FileInfo(Filename).Length);
        }

        [Test]
        public void only_top_n_checkpoints_are_stored_in_file()
        {
            _checkpoint.Update(EndPoint1, 1000);
            _checkpoint.Update(EndPoint2, 2000);
            _checkpoint.Update(EndPoint3, 3000);
            _checkpoint.Update(EndPoint4, 4000);

            _checkpoint.Flush();

            Assert.AreEqual((4 + 4 + 8) * 3, new FileInfo(Filename).Length);
        }

        [Test]
        public void should_return_single_available_value()
        {
            _checkpoint.Update(EndPoint1, 1000);

            long checkpoint;
            Assert.IsTrue(_checkpoint.TryGetMinMax(out checkpoint));
            Assert.AreEqual(1000, checkpoint);
        }

        [Test]
        public void can_update_inplace_for_present_endpoint_to_smaller_value()
        {
            _checkpoint.Update(EndPoint1, 1000);

            long checkpoint;
            Assert.IsTrue(_checkpoint.TryGetMinMax(out checkpoint));
            Assert.AreEqual(1000, checkpoint);

            _checkpoint.Update(EndPoint1, 500);

            Assert.IsTrue(_checkpoint.TryGetMinMax(out checkpoint));
            Assert.AreEqual(500, checkpoint);
        }

        [Test]
        public void can_update_inplace_for_present_endpoint_to_larger_value()
        {
            _checkpoint.Update(EndPoint1, 1000);

            long checkpoint;
            Assert.IsTrue(_checkpoint.TryGetMinMax(out checkpoint));
            Assert.AreEqual(1000, checkpoint);

            _checkpoint.Update(EndPoint1, 1500);

            Assert.IsTrue(_checkpoint.TryGetMinMax(out checkpoint));
            Assert.AreEqual(1500, checkpoint);
        }

        [Test]
        public void returns_min_among_two_values()
        {
            _checkpoint.Update(EndPoint1, 1000);
            _checkpoint.Update(EndPoint2, 500);

            long checkpoint;
            Assert.IsTrue(_checkpoint.TryGetMinMax(out checkpoint));
            Assert.AreEqual(500, checkpoint);
        }

        [Test]
        public void maintains_top_3_values_and_returns_min_of_them()
        {
            _checkpoint.Update(EndPoint1, 1000);
            _checkpoint.Update(EndPoint2, 2000);
            _checkpoint.Update(EndPoint3, 3000);
            _checkpoint.Update(EndPoint4, 4000);

            long checkpoint;
            Assert.IsTrue(_checkpoint.TryGetMinMax(out checkpoint));
            Assert.AreEqual(2000, checkpoint);
        }

        [Test]
        public void ignores_too_small_checkpoints_when_full()
        {
            _checkpoint.Update(EndPoint1, 1000);
            _checkpoint.Update(EndPoint2, 2000);
            _checkpoint.Update(EndPoint3, 3000);

            _checkpoint.Update(EndPoint4, 200);

            long checkpoint;
            Assert.IsTrue(_checkpoint.TryGetMinMax(out checkpoint));
            Assert.AreEqual(1000, checkpoint);
        }

        [Test]
        public void updates_inplace_to_smaller_value_when_full()
        {
            _checkpoint.Update(EndPoint1, 1000);
            _checkpoint.Update(EndPoint2, 2000);
            _checkpoint.Update(EndPoint3, 3000);

            _checkpoint.Update(EndPoint3, 200);

            long checkpoint;
            Assert.IsTrue(_checkpoint.TryGetMinMax(out checkpoint));
            Assert.AreEqual(200, checkpoint);
        }

        [Test]
        public void updates_inplace_to_larger_value_when_full()
        {
            _checkpoint.Update(EndPoint1, 1000);
            _checkpoint.Update(EndPoint2, 2000);
            _checkpoint.Update(EndPoint3, 3000);

            _checkpoint.Update(EndPoint1, 5000);

            long checkpoint;
            Assert.IsTrue(_checkpoint.TryGetMinMax(out checkpoint));
            Assert.AreEqual(2000, checkpoint);
        }

        [Test]
        public void has_no_checkpoints_to_return_after_clear()
        {
            _checkpoint.Update(EndPoint1, 1000);
            _checkpoint.Update(EndPoint2, 2000);
            _checkpoint.Update(EndPoint3, 3000);

            _checkpoint.Clear();

            long check;
            Assert.IsFalse(_checkpoint.TryGetMinMax(out check));
        }

        [Test]
        public void checkpoint_file_is_empty_after_clear_and_flush()
        {
            _checkpoint.Update(EndPoint1, 1000);
            _checkpoint.Update(EndPoint2, 2000);
            _checkpoint.Update(EndPoint3, 3000);

            _checkpoint.Clear();
            _checkpoint.Flush();

            long check;
            Assert.IsFalse(_checkpoint.TryGetMinMax(out check));
            Assert.AreEqual(0, new FileInfo(Filename).Length);
        }
    }
}
