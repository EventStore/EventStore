using System;
using EventStore.Common.Options;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.Options
{
    [TestFixture]
    public class opts_helper_for_int_should: OptsHelperTestBase
    {
        public int Value { get { throw new InvalidOperationException(); } }

        [Test]
        public void parse_explicitly_present_int_from_cmd_line()
        {
            Helper.Register(() => Value, "v|value=", "VALUE", "settings.value");
            
            Helper.Parse("-v", "123");
            Assert.AreEqual(123, Helper.Get(() => Value));
        }

        [Test]
        public void throw_option_exception_for_missing_value_with_no_default()
        {
            Helper.Register(() => Value, "v|value=", "VALUE", "settings.value");
            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void return_default_value_for_missing_value_if_default_is_set()
        {
            Helper.Register(() => Value, "v|value=", "VALUE", "settings.value", 100500);
            
            Helper.Parse();
            Assert.AreEqual(100500, Helper.Get(() => Value));
        }

        [Test]
        public void prefer_cmd_line_before_env()
        {
            Helper.Register(() => Value, "v|value=", "VALUE", "settings.value");
            SetEnv("VALUE", "123");

            Helper.Parse("--value=321");
            Assert.AreEqual(321, Helper.Get(() => Value));
        }

        [Test]
        public void prefer_cmd_line_before_json()
        {
            Helper.Register(() => Value, "v|value=", "VALUE", "settings.value");
            var cfg = WriteJsonConfig(new { settings = new { value = 123 } });
            
            Helper.Parse("-v", "321", "--cfg", cfg);
            Assert.AreEqual(321, Helper.Get(() => Value));
        }

        [Test]
        public void prefer_cmd_line_before_json_and_env()
        {
            Helper.Register(() => Value, "v|value=", "VALUE", "settings.value");
            SetEnv("VALUE", "123");
            var cfg = WriteJsonConfig(new { settings = new { value = 456 } });
            
            Helper.Parse("-v:321", "--cfg", cfg);
            Assert.AreEqual(321, Helper.Get(() => Value));
        }

        [Test]
        public void prefer_env_if_no_cmd_line()
        {
            Helper.Register(() => Value, "v|value=", "VALUE", "settings.value");
            SetEnv("VALUE", "123");
            var cfg = WriteJsonConfig(new { settings = new { value = 456 } });
            
            Helper.Parse("--cfg", cfg);
            Assert.AreEqual(123, Helper.Get(() => Value));
        }

        [Test]
        public void prefer_json_if_no_cmd_line_or_env()
        {
            Helper.Register(() => Value, "v|value=", "VALUE", "settings.value");
            var cfg = WriteJsonConfig(new { settings = new { value = 456 } });

            Helper.Parse("--cfg", cfg);
            Assert.AreEqual(456, Helper.Get(() => Value));
        }

        [Test]
        public void preserve_order_of_jsons()
        {
            Helper.Register(() => Value, "v|value=", "VALUE", "settings.value");
            var cfg1 = WriteJsonConfig(new { settings = new { value = 456 } });
            var cfg2 = WriteJsonConfig(new { settings = new { value = 789 } });

            Helper.Parse("--cfg", cfg1, "--cfg", cfg2);
            Assert.AreEqual(456, Helper.Get(() => Value));
        }

        [Test]
        public void search_all_jsons_before_giving_up()
        {
            Helper.Register(() => Value, "v|value=", "VALUE", "settings.value");
            var cfg1 = WriteJsonConfig(new { settings = new { value_other = 456 } });
            var cfg2 = WriteJsonConfig(new { settings = new { value = 789 } });

            Helper.Parse("--cfg", cfg1, "--cfg", cfg2);
            Assert.AreEqual(789, Helper.Get(() => Value));
        }

        [Test]
        public void use_default_if_all_failed()
        {
            Helper.Register(() => Value, "v|value=", "VALUE", "settings.value", 100500);
            var cfg1 = WriteJsonConfig(new { settings = new { value_other = false } });
            
            Helper.Parse("--cfg", cfg1);
            Assert.AreEqual(100500, Helper.Get(() => Value));
        }
    }
}