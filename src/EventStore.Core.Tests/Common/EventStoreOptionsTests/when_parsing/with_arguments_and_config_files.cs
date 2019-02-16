using EventStore.Common.Options;
using EventStore.Core.Util;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Tests.Helpers;

namespace EventStore.Core.Tests.Common.EventStoreOptionsTests.when_parsing {
	[TestFixture]
	public class with_arguments_and_config_files {
		[Test]
		public void should_use_the_argument_over_the_config_file_value() {
			var configPath = HelperExtensions.GetFilePathFromAssembly("TestConfigs/test_config.yaml");
			var args = new string[] {"-config", configPath, "-log", "~/customLogsDirectory"};
			var testArgs = EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);
			Assert.AreEqual("~/customLogsDirectory", testArgs.Log);
		}
	}
}
