using EventStore.Common.Options;
using EventStore.Core.Util;
using EventStore.Rags;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EventStore.Core.Tests.Common.EventStoreOptionsTests.when_updating
{
    [TestFixture]
    public class with_a_change_that_will_not_take_effect
    {
        private string tempFileName;
        [TestFixtureSetUp]
        public void Setup()
        {
            tempFileName = Path.GetTempPath() + Guid.NewGuid().ToString() + ".yaml";
            if (File.Exists(tempFileName))
            {
                File.Delete(tempFileName);
            }
        }
        [Test]
        public void should_throw_an_exception_containing_the_possible_conflicts()
        {
            File.WriteAllLines(tempFileName, new string[] { "HttpPort: 2113" });
            Environment.SetEnvironmentVariable((String.Format("{0}HTTP_PORT", Opts.EnvPrefix)), "2111", EnvironmentVariableTarget.Process);

            var args = new string[] { "--config=" + tempFileName, "--log=~/ouroLogs" };
            EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);

            var optionsToSave = new OptionSource[] { 
                OptionSource.Typed("Update", "HttpPort", 2115),
                OptionSource.Typed("Update", "Log", "~anotherLogLocation")
            };

            Assert.Throws<Exception>(() => { EventStoreOptions.Update(optionsToSave); });
        }
        [TestFixtureTearDown]
        public void Cleanup()
        {
             Environment.SetEnvironmentVariable((String.Format("{0}HTTP_PORT", Opts.EnvPrefix)), null, EnvironmentVariableTarget.Process);
            if (File.Exists(tempFileName))
            {
                File.Delete(tempFileName);
            }
        }
    }
}
