using EventStore.Common.Options;
using EventStore.Core.Util;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.Core.Tests.Common.EventStoreOptionsTests.when_parsing
{
    [TestFixture]
    public class with_incorrectly_cased_option_in_config
    {
        [Test]
        public void should_inform_the_user_of_the_incorrectly_cased_option()
        {
            var args = new string[] { "-config", "TestConfigs/test_config_with_incorrectly_cased_option.yaml" };
            var optionException = Assert.Throws<OptionException>(() => { EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix); });
            Assert.That(optionException.Message.Contains("log should be Log"));
            Assert.That(optionException.Message.Contains("runProjections should be RunProjections"));
        }
    }
}
