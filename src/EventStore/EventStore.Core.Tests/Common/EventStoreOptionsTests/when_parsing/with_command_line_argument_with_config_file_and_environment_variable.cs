using EventStore.Common.Options;
using EventStore.Core.Util;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventStore.Core.Tests.Common.EventStoreOptionsTests.when_parsing
{
    [TestFixture]
    public class with_command_line_argument_with_config_file_and_environment_variable
    {
        [Test]
        public void should_use_the_command_line_argument()
        {
            Environment.SetEnvironmentVariable("ES_HTTP_PORT", "2111");
            var args = new string[] { "-config", "TestConfigs/test_config.json", "-httpPort", "2115" };
            var testArgs = EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);
            Assert.AreEqual(2115, testArgs.HttpPort);
        }
    }
}
