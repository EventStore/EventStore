using System;
using EventStore.Common.Options;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.Options
{
    [TestFixture]
    public class opts_helper_should_report_parse_errors_for_flag : OptsHelperTestBase
    {
        public bool Flag { get { throw new InvalidOperationException(); } }
        
        [Test, Ignore("Mono.Options allows this situation and ignores the value provided.")]
        public void with_value_in_cmd_line()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag");

            Helper.Parse("-f", "somevalue");
            Assert.Fail();
        }

        [Test, Ignore("Mono.Options allows this situation and ignores the value provided.")]
        public void if_flag_is_defined_more_than_once()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag");

            Assert.Throws<OptionException>(() => Helper.Parse("-f-", "-f+"));
        }

        [Test]
        public void with_non_bool_value_in_env()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag");
            SetEnv("FLAG", "NOTBOOL");

            Assert.Throws<OptionException>(() => Helper.Parse(),
                                           "Invalid value for flag in environment variable OPTSHELPER_FLAG: 'NOTBOOL', valid values are '0' and '1'.");
        }

        [Test]
        public void with_wrong_type_in_json()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag");
            var cfg = WriteJsonConfig(new {settings = new {flag = "bla"}});

            Assert.Throws<OptionException>(() => Helper.Parse("--cfg", cfg));
        }
    }
}