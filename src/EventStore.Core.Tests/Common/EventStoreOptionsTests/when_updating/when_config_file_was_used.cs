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
    public class when_config_file_was_used
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
        public void should_save_the_single_change_in_the_config_file()
        {
            var args = new string[] { "--config=" + tempFileName };
            File.WriteAllLines(tempFileName, new string[]{
                "RunProjections: All",
                "HttpPort: 2113",
                "Log: ~/ouroLogs"});

            EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);

            var optionsToSave = new OptionSource[] { 
                OptionSource.Typed("Update", "HttpPort", 2115),
            };

            var updatedOptions = EventStoreOptions.Update(optionsToSave);
            var optionsFromConfig = EventStoreOptions.Parse<TestArgs>(tempFileName);

            Assert.AreEqual(1, updatedOptions.Count());

            Assert.AreEqual(ProjectionType.All, optionsFromConfig.RunProjections);
            Assert.AreEqual(2115, optionsFromConfig.HttpPort);
            Assert.AreEqual("~/ouroLogs", optionsFromConfig.Log);
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
