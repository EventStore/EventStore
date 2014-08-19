using System;
using System.Collections.Generic;
using System.Text;
using EventStore.Core.Services.PersistentSubscription;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services
{
    [TestFixture]
    public class PersistentSubscriptionConfigTests
    {
        [Test]
        public void output_can_be_read_as_input_and_keep_same_values()
        {
            var config = new PersistentSubscriptionConfig();
            config.Updated = new DateTime(2014,08, 14);
            config.UpdatedBy = "Greg";
            config.Version = "1";
            config.Entries = new List<PersistentSubscriptionEntry>();
            config.Entries.Add(new PersistentSubscriptionEntry(){Group="foo", ResolveLinkTos = true, Stream = "Stream"});
            var data = config.GetSerializedForm();
            var config2 = PersistentSubscriptionConfig.FromSerializedForm(data);
            Assert.AreEqual(1, config2.Entries.Count);
            Assert.AreEqual(config.Updated, config2.Updated);
            Assert.AreEqual(config.UpdatedBy, config2.UpdatedBy);
        }

        [Test]
        public void bunk_json_causes_bad_config_data_exception()
        {
            var bunkdata = Encoding.UTF8.GetBytes("{'some weird shit' : 'something'}");
            Assert.Throws<BadConfigDataException>(() => PersistentSubscriptionConfig.FromSerializedForm(bunkdata));
        }

        [Test]
        public void random_crap_data_causes_bad_config_data_exception()
        {
            var bunkdata = Encoding.UTF8.GetBytes("This ain't even valid json");
            Assert.Throws<BadConfigDataException>(() => PersistentSubscriptionConfig.FromSerializedForm(bunkdata));
        }
    }
}