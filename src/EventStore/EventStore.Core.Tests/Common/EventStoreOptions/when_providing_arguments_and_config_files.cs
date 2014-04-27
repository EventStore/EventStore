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
    public class when_providing_arguments_and_config_files
    {
        private static TestArgs testArgs;
        private static string[] args;
        [Test]
        public void should_use_the_argument_over_the_config_file_value()
        {
            args = new string[] { "-config", "test_config.json", "-logsdir", "~/customLogsDirectory" };
            testArgs = EventStoreOptions.Parse<TestArgs>(args);
            Assert.AreEqual("~/customLogsDirectory", testArgs.Logsdir);
        }
    }
}
