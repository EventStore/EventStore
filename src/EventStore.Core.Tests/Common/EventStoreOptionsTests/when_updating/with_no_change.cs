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
    public class with_no_change
    {
        private string tempFileName;
        [TestFixtureSetUp]
        public void Setup()
        {
            tempFileName = Path.GetTempPath() + "with_no_change.yaml";
            if (File.Exists(tempFileName))
            {
                File.Delete(tempFileName);
            }
        }
        [Test]
        public void should_save_no_changes()
        {
            var args = new string[] { };
            EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);

            var optionsToSave = new OptionSource[] { 
                OptionSource.Typed("<DEFAULT>", "Help", false),
                OptionSource.Typed("<DEFAULT>", "Version", false),
                OptionSource.Typed("<DEFAULT>", "Config", null),
                OptionSource.Typed("<DEFAULT>", "HttpPort", 2112),
                OptionSource.Typed("<DEFAULT>", "Log", "~/logs"),
                OptionSource.Typed("<DEFAULT>", "Defines", null),
                OptionSource.Typed("<DEFAULT>", "GossipSeed", null),
                OptionSource.Typed("<DEFAULT>", "WhatIf", false),
                OptionSource.Typed("<DEFAULT>", "Force", false),
                OptionSource.Typed("<DEFAULT>", "RunProjections", ProjectionType.None)
            };

            var savedOptions = EventStoreOptions.Update(optionsToSave, tempFileName);

            Assert.AreEqual(1, savedOptions.Count());
            Assert.AreEqual("Config", savedOptions.First().Name);
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
