using EventStore.Common.Options;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventStore.Core.Tests.Common.when_parsing_options
{
    [TestFixture]
    public class when_providing_an_config_file_and_environment_variable
    {
        private static TestArgs testArgs;
        private static string[] args;
        [Test]
        public void should_use_the_config_value_over_the_environment_variable()
        {
            Environment.SetEnvironmentVariable("ES_HTTP_PORT", "2111");
            args = new string[] { "-config", "test_config.json" };
            testArgs = EventStoreOptions.Parse<TestArgs>(args);
            Assert.AreEqual(2115, testArgs.HttpPort);
        }
    }
}
