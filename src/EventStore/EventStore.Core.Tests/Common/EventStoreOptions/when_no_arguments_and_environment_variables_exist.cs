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
    public class when_no_arguments_and_environment_variables_exist
    {
        private static TestArgs testArgs;
        private static string[] args;
        [Test]
        public void should_use_the_environment_variable_over_the_default_value()
        {
            Environment.SetEnvironmentVariable("ES_HTTP_PORT", "2111");
            testArgs = EventStoreOptions.Parse<TestArgs>(args);
            Assert.AreEqual(2111, testArgs.HttpPort);
        }
    }
}
