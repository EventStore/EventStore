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
            tempFileName = Path.GetTempPath() + "with_a_single_change.yaml";
            if (File.Exists(tempFileName))
            {
                File.Delete(tempFileName);
            }
        }
        [Test]
        public void should_save_the_single_change()
        {
            var args = new string[] { };
            EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);

            var optionsToSave = new OptionSource[] { 
                OptionSource.Typed("<DEFAULT>", "Help", false),
                OptionSource.Typed("<DEFAULT>", "Version", false),
                OptionSource.Typed("<DEFAULT>", "Config", null),
                OptionSource.Typed("<DEFAULT>", "HttpPort", 2113),
                OptionSource.Typed("<DEFAULT>", "Log", "~/logs"),
                OptionSource.Typed("<DEFAULT>", "Defines", null),
                OptionSource.Typed("<DEFAULT>", "GossipSeed", null),
                OptionSource.Typed("<DEFAULT>", "WhatIf", false),
                OptionSource.Typed("<DEFAULT>", "Force", false),
                OptionSource.Typed("<DEFAULT>", "RunProjections", ProjectionType.None)
            };

            var savedOptions = EventStoreOptions.Update(optionsToSave, tempFileName);

            Assert.AreEqual(2, savedOptions.Count());

            Assert.AreEqual("Config", savedOptions.First().Name);

            Assert.AreEqual("HttpPort", savedOptions.Last().Name);
            Assert.AreEqual(2113, savedOptions.Last().Value);
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
