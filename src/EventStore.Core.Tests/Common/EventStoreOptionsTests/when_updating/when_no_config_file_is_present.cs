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
    public class when_no_config_file_is_present
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
        public void should_throw_an_exception_explaining_that_an_valid_config_file_is_needed()
        {
            var args = new string[] { };
            EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);

            var optionsToSave = new OptionSource[] { 
                OptionSource.Typed("Update", "HttpPort", 2115),
            };

           Assert.Throws<Exception>(() => { EventStoreOptions.Update(optionsToSave); });
        }
        [TestFixtureTearDown]
        public void Cleanup()
        {
            if (File.Exists(tempFileName))
            {
                File.Delete(tempFileName);
            }
        }
    }
}
