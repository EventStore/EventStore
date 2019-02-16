using EventStore.Common.Options;
using EventStore.Core.Util;
using EventStore.Rags;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.Core.Tests.Helpers;


namespace EventStore.Core.Tests.Common.EventStoreOptionsTests.when_parsing {
	[TestFixture, Category("LongRunning")]
	public class with_unknown_options {
		[Test]
		public void should_warn_the_user_about_unknown_argument_when_from_command_line() {
			var args = new string[] {"-unknown-option", "true"};
			var optionException = Assert.Throws<OptionException>(() => {
				EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);
			});
			Assert.True(optionException.Message.Contains("unknownoption"));
		}

		[Test]
		public void should_warn_the_user_about_unknown_argument_when_from_config_file() {
			var configFile =
				HelperExtensions.GetFilePathFromAssembly("TestConfigs/test_config_with_unknown_option.yaml");
			var args = new string[] {"-config", configFile};
			var optionException = Assert.Throws<OptionException>(() => {
				EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);
			});
			Assert.True(optionException.Message.Contains("UnknownOption"));
		}

		[Test]
		public void should_not_contain_the_unknown_option_in_the_dumping_of_the_options_environment_variable() {
			Environment.SetEnvironmentVariable(String.Format("{0}UNKNOWN_OPTION", Opts.EnvPrefix), "true");
			var args = new string[] { };
			EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);

			Assert.False(EventStoreOptions.DumpOptions().Contains(String.Format("{0}UNKNOWN_OPTION", Opts.EnvPrefix)));
		}
	}
}
