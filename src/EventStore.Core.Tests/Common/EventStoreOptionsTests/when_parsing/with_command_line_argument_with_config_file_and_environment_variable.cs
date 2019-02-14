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
	public class with_command_line_argument_with_config_file_and_environment_variable {
		[Test]
		public void should_use_the_command_line_argument() {
			var configFile = HelperExtensions.GetFilePathFromAssembly("TestConfigs/test_config.yaml");
			Environment.SetEnvironmentVariable(String.Format("{0}HTTP_PORT", Opts.EnvPrefix), "2111",
				EnvironmentVariableTarget.Process);
			var args = new string[] {"-config", configFile, "-httpPort", "2115"};
			var testArgs = EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);
			Assert.AreEqual(2115, testArgs.HttpPort);
			Environment.SetEnvironmentVariable(String.Format("{0}HTTP_PORT", Opts.EnvPrefix), null,
				EnvironmentVariableTarget.Process);
		}
	}
}
