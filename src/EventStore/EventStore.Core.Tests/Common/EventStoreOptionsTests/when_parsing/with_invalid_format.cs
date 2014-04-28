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
    public class with_invalid_format
    {
        [Test]
        public void with_command_line_argument()
        {
            var args = new string[] { "-httpPort", "invalid_format" };
            Assert.Throws<OptionException>(() => { EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix); });
        }
        [Test]
        public void with_config()
        {
            var args = new string[] { "-config", "TestConfigs/invalid_format_config.json" };
            Assert.Throws<OptionException>(() => { EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix); });
        }
        [Test]
        public void with_environment_variable()
        {
            Environment.SetEnvironmentVariable(Opts.EnvPrefix + "HTTP_PORT", "invalid_format");
            var args = new string[] { };
            Assert.Throws<OptionException>(() => { EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix); });
            Environment.SetEnvironmentVariable(Opts.EnvPrefix + "HTTP_PORT", null);
        }
    }
}
