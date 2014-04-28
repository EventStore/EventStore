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
    public class with_invalid_format
    {
        [Test]
        public void with_command_line_argument()
        {
            var args = new string[] { "-httpPort", "invalidformat" };
            Assert.Throws<OptionException>(() => { EventStoreOptions.Parse<TestArgs>(args); });
        }
        [Test]
        public void with_config()
        {
            var args = new string[] { "-config", "TestConfigs/invalid_format_config.json" };
            Assert.Throws<OptionException>(() => { EventStoreOptions.Parse<TestArgs>(args); });
        }
        [Test]
        public void with_environment_variable()
        {
            Environment.SetEnvironmentVariable("ES_HTTP_PORT", "invalid_format");
            var args = new string[] { };
            Assert.Throws<OptionException>(() => { EventStoreOptions.Parse<TestArgs>(args); });
            Environment.SetEnvironmentVariable("ES_HTTP_PORT", null);
        }
    }
}
