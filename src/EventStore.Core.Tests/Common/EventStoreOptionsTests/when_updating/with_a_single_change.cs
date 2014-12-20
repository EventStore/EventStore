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
    public class with_a_single_change
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
        public void should_save_the_single_change()
        {
            File.WriteAllLines(tempFileName, new string[]{
                "RunProjections: All",
                "HttpPort: 2113",
                "Log: ~/ouroLogs"});

            var args = new string[] { "--config=" + tempFileName };
            EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);

            var optionsToSave = new OptionSource[] { 
                OptionSource.Typed("Update", "HttpPort", 2115),
            };

            EventStoreOptions.Update(optionsToSave);

            var savedConfig = Yaml.FromFile(tempFileName);
            Assert.AreEqual(3, savedConfig.Count());
            Assert.AreEqual("All", savedConfig.First(x => x.Name == "RunProjections").Value);
            Assert.AreEqual("2115", savedConfig.First(x => x.Name == "HttpPort").Value);
            Assert.AreEqual("~/ouroLogs", savedConfig.First(x => x.Name == "Log").Value);
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
