using System;
using EventStore.Common.Options;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.Options
{
    [TestFixture]
    public class opts_helper_should_report_parse_errors_for_number: OptsHelperTestBase
    {
        public int Number { get { throw new InvalidOperationException(); } }

        [Test]
        public void with_no_value_in_cmd_line()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");

            Assert.Throws<OptionException>(() => Helper.Parse("-n"));
        }

        [Test]
        public void with_non_numeric_value_in_cmd_line()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");

            Assert.Throws<OptionException>(() => Helper.Parse("-n", "not-a-number"));
        }

        [Test]
        public void with_overflow_in_cmd_line()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");

            Assert.Throws<OptionException>(() => Helper.Parse("-n", "123123123123123123"));
        }

        [Test]
        public void with_floating_point_in_cmd_line()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");

            Assert.Throws<OptionException>(() => Helper.Parse("-n", "123.123"));
        }

        [Test]
        public void with_non_numeric_value_in_env()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");
            SetEnv("NUM", "ABC");

            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void with_overflow_in_env()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");
            SetEnv("NUM", "123123123123123123");
            
            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void with_floating_point_in_env()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");
            SetEnv("NUM", "123.123");

            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void with_no_value_in_json()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");
            var cfg = WriteJsonConfig(new {settings = new {num = new {}}});
            
            Assert.Throws<OptionException>(() => Helper.Parse("--cfg", cfg));
        }

        [Test]
        public void with_non_numeric_value_in_json()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");
            var cfg = WriteJsonConfig(new { settings = new { num = "abc" } });

            Assert.Throws<OptionException>(() => Helper.Parse("--cfg", cfg));
        }

        [Test]
        public void with_overflow_in_json()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");
            var cfg = WriteJsonConfig(new { settings = new { num = 123123123123123L } });

            Assert.Throws<OptionException>(() => Helper.Parse("--cfg", cfg));
        }

        [Test, Ignore("If this is really needed, we have to make additional checks, for now I'm leaving it with default automatic conversion.")]
        public void with_floating_point_in_json()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");
            var cfg = WriteJsonConfig(new { settings = new { num = 123.123 } });

            Helper.Parse("--cfg", cfg);
            Assert.Fail();
        }
    }
}