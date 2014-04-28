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
    public class with_a_config_file
    {
        [Test]
        public void should_use_the_config_file_value()
        {
            var args = new string[] { "-config", "TestConfigs/test_config.json" };
            var testArgs = EventStoreOptions.Parse<TestArgs>(args);
            Assert.AreEqual("~/logDirectoryFromConfigFile", testArgs.Logsdir);
        }
    }
}
