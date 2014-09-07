using System;
using System.Linq;
using EventStore.Core.Services.PersistentSubscription;
using EventStore.Core.Tests.Services.PersistentSubscriptionTests;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.PersistentSubscription
{
    [TestFixture]
    public class OutstandingMessageCacheTests
    {
        [Test]
        public void when_created_has_zero_count()
        {
            var cache = new OutstandingMessageCache();
            Assert.AreEqual(0, cache.Count);
        }

        [Test]
        public void can_remove_non_existing_item()
        {
            var cache = new OutstandingMessageCache();
            Assert.DoesNotThrow(() => cache.Remove(Guid.NewGuid()));
        }

        [Test]
        public void adding_an_item_causes_count_to_go_up()
        {
            var id = Guid.NewGuid();
            var cache = new OutstandingMessageCache();
            cache.StartMessage(new OutstandingMessage(id, null, Helper.BuildFakeEvent(id, "type", "name", 0), 0), DateTime.Now);
            Assert.AreEqual(1, cache.Count);
            Assert.AreEqual(0, cache.GetLowestPosition());
        }

        [Test]
        public void can_add_duplicate()
        {
            var id = Guid.NewGuid();
            var cache = new OutstandingMessageCache();
            cache.StartMessage(new OutstandingMessage(id, null, Helper.BuildFakeEvent(id, "type", "name", 0), 0), DateTime.Now);
            cache.StartMessage(new OutstandingMessage(id, null, Helper.BuildFakeEvent(id, "type", "name", 0), 0), DateTime.Now);
            Assert.AreEqual(1, cache.Count);
            Assert.AreEqual(0, cache.GetLowestPosition());
        }

        [Test]
        public void can_remove_existing_item()
        {
            var id = Guid.NewGuid();
            var cache = new OutstandingMessageCache();
            cache.StartMessage(new OutstandingMessage(id, null, Helper.BuildFakeEvent(id, "type", "name", 0), 0), DateTime.Now);
            cache.Remove(id);
            Assert.AreEqual(0, cache.Count);
        }

        [Test]
        public void get_expired_messages_returns_none_on_empty_cache()
        {
            var cache = new OutstandingMessageCache();
            Assert.AreEqual(0, cache.GetMessagesExpiringBefore(DateTime.Now).Count());
            Assert.AreEqual(int.MaxValue, cache.GetLowestPosition());
        }

        [Test]
        public void message_that_expires_is_included_in_expired_list()
        {
            var id = Guid.NewGuid();
            var cache = new OutstandingMessageCache();
            cache.StartMessage(new OutstandingMessage(id, null, Helper.BuildFakeEvent(id, "type", "name", 0), 0), DateTime.Now.AddSeconds(-1));
            var expired = cache.GetMessagesExpiringBefore(DateTime.Now).ToList();
            Assert.AreEqual(1, expired.Count());
            Assert.AreEqual(id, expired.FirstOrDefault().MessageId);
        }

        [Test]
        public void message_that_expires_is_included_in_expired_list_with_another_that_should_not()
        {
            var id = Guid.NewGuid();
            var cache = new OutstandingMessageCache();
            cache.StartMessage(new OutstandingMessage(id, null, Helper.BuildFakeEvent(id, "type", "name", 0), 0), DateTime.Now.AddSeconds(-1));
            cache.StartMessage(new OutstandingMessage(Guid.NewGuid(), null, Helper.BuildFakeEvent(Guid.NewGuid(), "type", "name", 1), 0), DateTime.Now.AddSeconds(1));
            var expired = cache.GetMessagesExpiringBefore(DateTime.Now).ToList();
            Assert.AreEqual(1, expired.Count());
            Assert.AreEqual(id, expired.FirstOrDefault().MessageId);
        }

        [Test]
        public void message_that_notexpired_is_not_included_in_expired_list()
        {
            var id = Guid.NewGuid();
            var cache = new OutstandingMessageCache();
            cache.StartMessage(new OutstandingMessage(id, null, Helper.BuildFakeEvent(id, "type", "name", 0), 0), DateTime.Now.AddSeconds(1));
            var expired = cache.GetMessagesExpiringBefore(DateTime.Now).ToList();
            Assert.AreEqual(0, expired.Count());
        }
    }
}
