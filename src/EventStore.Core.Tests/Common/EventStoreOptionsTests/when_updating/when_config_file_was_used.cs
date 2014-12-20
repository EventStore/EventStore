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
                "HttpPort: 2113"});

            EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);

            var optionsToSave = new OptionSource[] { 
                OptionSource.Typed("Update", "HttpPort", 2115),
            };

            EventStoreOptions.Update(optionsToSave);

            var savedConfig = Yaml.FromFile(tempFileName);
            Assert.AreEqual(2, savedConfig.Count());
            Assert.AreEqual("All", savedConfig.First(x => x.Name == "RunProjections").Value);
            Assert.AreEqual("2115", savedConfig.First(x => x.Name == "HttpPort").Value);
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
