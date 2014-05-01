using EventStore.Common.Options;
using EventStore.Core.Util;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.Core.Tests.Common.EventStoreOptionsTests.when_parsing
{
    [TestFixture]
    public class with_long_form_argument
    {
        [Test]
        public void should_use_the_supplied_argument()
        {
            var args = new string[] { "--logsdir=~/customLogDirectory" };
            var testArgs = EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);
            Assert.AreEqual("~/customLogDirectory", testArgs.Logsdir);
        }
    }
}
