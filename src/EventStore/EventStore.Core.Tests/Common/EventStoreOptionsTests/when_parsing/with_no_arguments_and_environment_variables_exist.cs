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
    public class with_no_arguments_and_environment_variables_exist
    {
        [Test]
        public void should_use_the_environment_variable_over_the_default_value()
        {
            Environment.SetEnvironmentVariable("ES_HTTP_PORT", "2111");
            var args = new string[] { };
            var testArgs = EventStoreOptions.Parse<TestArgs>(args);
            Assert.AreEqual(2111, testArgs.HttpPort);
        }
    }
}
