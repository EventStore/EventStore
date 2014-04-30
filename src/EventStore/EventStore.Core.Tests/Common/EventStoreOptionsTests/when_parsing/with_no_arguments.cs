﻿using EventStore.Common.Options;
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
    public class with_no_arguments
    {
        [Test]
        public void should_use_the_defaults()
        {
            var args = new string[] { };
            var testArgs = EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);
            Assert.AreEqual("~/logs", testArgs.Logsdir);
        }
    }
}
