﻿using System;
using System.Linq;
using EventStore.Core.Services.PersistentSubscription;
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
            var result1 = cache.StartMessage(new OutstandingMessage(id, null, Helper.BuildFakeEvent(id, "type", "name", 0), 0), DateTime.Now);
            var result2 = cache.StartMessage(new OutstandingMessage(id, null, Helper.BuildFakeEvent(id, "type", "name", 1), 0), DateTime.Now);
            Assert.AreEqual(1, cache.Count);
            Assert.AreEqual(0, cache.GetLowestPosition());
            Assert.AreEqual(StartMessageResult.Success, result1);
            Assert.AreEqual(StartMessageResult.SkippedDuplicate, result2);
        }

        [Test]
        public void can_remove_duplicate()
        {
            var id = Guid.NewGuid();
            var cache = new OutstandingMessageCache();
            cache.StartMessage(new OutstandingMessage(id, null, Helper.BuildFakeEvent(id, "type", "name", 0), 0), DateTime.Now);
            cache.StartMessage(new OutstandingMessage(id, null, Helper.BuildFakeEvent(id, "type", "name", 1), 0), DateTime.Now);
            cache.Remove(id);
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(int.MinValue, cache.GetLowestPosition());
        }

        [Test]
        public void can_remove_existing_item()
        {
            var id = Guid.NewGuid();
            var cache = new OutstandingMessageCache();
            cache.StartMessage(new OutstandingMessage(id, null, Helper.BuildFakeEvent(id, "type", "name", 0), 0), DateTime.Now);
            cache.Remove(id);
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(int.MinValue, cache.GetLowestPosition());
        }

        [Test]
        public void lowest_works_on_add()
        {
            var id = Guid.NewGuid();
            var cache = new OutstandingMessageCache();
            cache.StartMessage(new OutstandingMessage(id, null, Helper.BuildFakeEvent(id, "type", "name", 10), 0), DateTime.Now);
            Assert.AreEqual(10, cache.GetLowestPosition());
        }

        [Test]
        public void lowest_works_on_adds_then_remove()
        {
            var id = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();
            var cache = new OutstandingMessageCache();
            cache.StartMessage(new OutstandingMessage(id, null, Helper.BuildFakeEvent(id, "type", "name", 10), 0), DateTime.Now);
            cache.StartMessage(new OutstandingMessage(id2, null, Helper.BuildFakeEvent(id2, "type", "name", 11), 0), DateTime.Now);
            cache.StartMessage(new OutstandingMessage(id3, null, Helper.BuildFakeEvent(id3, "type", "name", 12), 0), DateTime.Now);
            cache.Remove(id);
            Assert.AreEqual(11, cache.GetLowestPosition());
        }

        [Test]
        public void lowest_on_empty_cache_returns_min()
        {
            var cache = new OutstandingMessageCache();
            Assert.AreEqual(int.MinValue, cache.GetLowestPosition());
        }
        [Test]
        public void get_expired_messages_returns_min_value_on_empty_cache()
        {
            var cache = new OutstandingMessageCache();
            Assert.AreEqual(0, cache.GetMessagesExpiringBefore(DateTime.Now).Count());
            Assert.AreEqual(int.MinValue, cache.GetLowestPosition());
        }

        [Test]
        public void message_that_expires_is_included_in_expired_list()
        {
            var id = Guid.NewGuid();
            var cache = new OutstandingMessageCache();
            cache.StartMessage(new OutstandingMessage(id, null, Helper.BuildFakeEvent(id, "type", "name", 0), 0), DateTime.Now.AddSeconds(-1));
            var expired = cache.GetMessagesExpiringBefore(DateTime.Now).ToList();
            Assert.AreEqual(1, expired.Count());
            Assert.AreEqual(id, expired.FirstOrDefault().EventId);
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
            Assert.AreEqual(id, expired.FirstOrDefault().EventId);
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
