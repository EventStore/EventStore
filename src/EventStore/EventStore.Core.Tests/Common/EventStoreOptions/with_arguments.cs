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
    public class with_arguments
    {
        private static TestArgs testArgs;
        private static string[] args;
        [Test]
        public void should_use_the_supplied_argument()
        {
            args = new string[] { "-logsdir", "~/customLogDirectory" };
            testArgs = EventStoreOptions.Parse<TestArgs>(args);
            Assert.AreEqual("~/customLogDirectory", testArgs.Logsdir);
        }
    }
}
