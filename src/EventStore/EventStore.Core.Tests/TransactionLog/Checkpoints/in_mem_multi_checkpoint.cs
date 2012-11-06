using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Checkpoints
{
    [TestFixture]
    public class in_mem_multi_checkpoint
    {
        private static readonly IPEndPoint EndPoint1 = new IPEndPoint(IPAddress.Loopback, 1000);
        private static readonly IPEndPoint EndPoint2 = new IPEndPoint(IPAddress.Loopback, 2000);
        private static readonly IPEndPoint EndPoint3 = new IPEndPoint(IPAddress.Loopback, 3000);
        private static readonly IPEndPoint EndPoint4 = new IPEndPoint(IPAddress.Loopback, 4000);

        private InMemMultiCheckpoint _checkpoint;

        [SetUp]
        public void SetUp()
        {
            _checkpoint = new InMemMultiCheckpoint(3);
        }

        [Test]
        public void should_not_return_checkpoint_when_empty()
        {
            long checkpoint;
            Assert.IsFalse(_checkpoint.TryGetMinMax(out checkpoint));
            Assert.AreEqual(0, checkpoint);
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

        [TearDown]
        public void TearDown()
        {
            
        }
    }
}
