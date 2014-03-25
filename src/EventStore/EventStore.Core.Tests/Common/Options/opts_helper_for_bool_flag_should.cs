using System;
using EventStore.Common.Options;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.Options
{
    [TestFixture]
    public class opts_helper_for_bool_flag_should: OptsHelperTestBase
    {
        public bool Flag { get { throw new InvalidOperationException(); } }

        [Test]
        public void parse_explicitly_present_bool_from_cmd_line()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag");
            
            Helper.Parse("-f");
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void parse_explicitly_present_negative_flag_from_cmd_line()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag");
            
            Helper.Parse("-f-");
            Assert.IsFalse(Helper.Get(() => Flag));
        }

        [Test]
        public void throw_option_exception_for_missing_flag_with_no_default()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag");
            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void return_default_value_for_missing_flag_if_default_is_set()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag", true);
            
            Helper.Parse();
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void prefer_cmd_line_before_env()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag");
            SetEnv("FLAG", "0");

            Helper.Parse("-f");
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void prefer_cmd_line_before_json()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag");
            var cfg = WriteJsonConfig(new { settings = new { flag = false } });
            
            Helper.Parse("-f", "--cfg", cfg);
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void prefer_cmd_line_before_json_and_env()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag");
            SetEnv("FLAG", "0");
            var cfg = WriteJsonConfig(new { settings = new { flag = false } });
            
            Helper.Parse("-f", "--cfg", cfg);
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void prefer_env_if_no_cmd_line()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag");
            SetEnv("FLAG", "1");
            var cfg = WriteJsonConfig(new { settings = new { flag = false } });
            
            Helper.Parse("--cfg", cfg);
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void prefer_json_if_no_cmd_line_or_env()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag");
            var cfg = WriteJsonConfig(new { settings = new { flag = true } });

            Helper.Parse("--cfg", cfg);
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void preserve_order_of_jsons()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag");
            var cfg1 = WriteJsonConfig(new { settings = new { flag = false } });
            var cfg2 = WriteJsonConfig(new { settings = new { flag = true } });

            Helper.Parse("--cfg", cfg1, "--cfg", cfg2);
            Assert.IsFalse(Helper.Get(() => Flag));
        }

        [Test]
        public void search_all_jsons_before_giving_up()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag");
            var cfg1 = WriteJsonConfig(new { settings = new { flag_other = false } });
            var cfg2 = WriteJsonConfig(new { settings = new { flag = true } });

            Helper.Parse("--cfg", cfg1, "--cfg", cfg2);
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void use_default_if_all_failed()
        {
            Helper.Register(() => Flag, "f|flag", "FLAG", "settings.flag", true);
            var cfg1 = WriteJsonConfig(new { settings = new { flag_other = false } });
            
            Helper.Parse("--cfg", cfg1);
            Assert.IsTrue(Helper.Get(() => Flag));
        }
    }
}