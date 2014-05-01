using EventStore.Common.Options;
using EventStore.Core.Util;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.EventStoreOptionsTests.when_parsing
{
    [TestFixture]
    public class with_long_form_argument
    {
        [Test]
        public void should_use_the_supplied_argument()
        {
            var args = new[] { "--logsdir=~/customLogDirectory" };
            var testArgs = EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);
            Assert.AreEqual("~/customLogDirectory", testArgs.Logsdir);
        }

        [Test]
        public void should_not_require_equals_in_method()
        {
            var args = new[] {"--logsdir", "./customLogDirectory", "--run-projections", "all"};
            var testArgs = EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);
            Assert.AreEqual("./customLogDirectory", testArgs.Logsdir);
            Assert.AreEqual(ProjectionType.All, testArgs.RunProjections);
        }
    }
}
