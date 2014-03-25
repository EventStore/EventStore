using System;
using EventStore.Common.Options;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.Options
{
    [TestFixture]
    public class opts_helper_should_report_parse_errors_for_array : OptsHelperTestBase
    {
        public int[] Array { get { throw new InvalidOperationException(); } }

        [Test]
        public void with_no_value_in_cmd_line()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);

            Assert.Throws<OptionException>(() => Helper.Parse("-a"));
        }

        [Test]
        public void with_wrong_format_of_element_in_cmd_line()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);

            Assert.Throws<OptionException>(() => Helper.Parse("-a", "123", "-a", "abc"));
        }

        [Test]
        public void with_wrong_format_of_element_in_env()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);
            SetEnv("ARR", "127,abc");

            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void with_missing_elements_in_env()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);
            SetEnv("ARR", "127,,721");

            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void with_wrong_type_in_json()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);
            var cfg = WriteJsonConfig(new { settings = new { arr = new { } } });

            Assert.Throws<OptionException>(() => Helper.Parse("--cfg", cfg));
        }

        [Test]
        public void with_string_instead_of_array_in_json()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);
            var cfg = WriteJsonConfig(new { settings = new { arr = "1,2,3" } });

            Assert.Throws<OptionException>(() => Helper.Parse("--cfg", cfg));
        }

        [Test]
        public void with_wrong_format_of_element_in_json()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);
            var cfg = WriteJsonConfig(new {settings = new {arr = new object[] {123, "abc"}}});

            Assert.Throws<OptionException>(() => Helper.Parse("--cfg", cfg));
        }
    }
}