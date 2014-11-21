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
    public class with_a_single_change_when_no_config_file_is_specified
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
        public void should_save_the_changes_to_the_default_config_file()
        {
            var args = new string[] { };
            EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);

            var optionsToSave = new OptionSource[] { 
                OptionSource.Typed("Update", "HttpPort", 2115),
            };

            var savedOptions = EventStoreOptions.Update(optionsToSave, tempFileName);

            Assert.AreEqual(1, savedOptions.Count());

            Assert.AreEqual("HttpPort", savedOptions.Last().Name);
            Assert.AreEqual(2115, savedOptions.Last().Value);
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
