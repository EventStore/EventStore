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
    public class with_arguments
    {
        [Test]
        public void should_use_the_supplied_argument()
        {
            var args = new string[] { "-logsdir", "~/customLogDirectory" };
            var testArgs = EventStoreOptions.Parse<TestArgs>(args);
            Assert.AreEqual("~/customLogDirectory", testArgs.Logsdir);
        }
    }
}
