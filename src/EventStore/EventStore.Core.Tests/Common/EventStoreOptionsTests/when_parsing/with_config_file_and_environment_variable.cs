using EventStore.Common.Options;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventStore.Core.Tests.Common.EventStoreOptionsTests.when_parsing
{
    [TestFixture]
    public class with_config_file_and_environment_variable
    {
        [Test]
        public void should_use_the_config_value_over_the_environment_variable()
        {
            Environment.SetEnvironmentVariable("ES_HTTP_PORT", "2111");
            var args = new string[] { "-config", "TestConfigs/test_config.json" };
            var testArgs = EventStoreOptions.Parse<TestArgs>(args);
            Assert.AreEqual(2115, testArgs.HttpPort);
        }
    }
}
