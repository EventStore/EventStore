using System;
using System.Net;
using EventStore.Common.Options;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.Options
{
    [TestFixture]
    public class opts_helper_for_custom_type_should: OptsHelperTestBase
    {
        public IPAddress Value { get { throw new InvalidOperationException(); } }

        [Test]
        public void parse_explicitly_present_value_from_cmd_line()
        {
            Helper.RegisterRef(() => Value, "v|value=", "VALUE", "settings.value");
            
            Helper.Parse("-v", "192.168.1.1");
            Assert.AreEqual(IPAddress.Parse("192.168.1.1"), Helper.Get(() => Value));
        }

        [Test]
        public void throw_option_exception_for_missing_value_with_no_default()
        {
            Helper.RegisterRef(() => Value, "v|value=", "VALUE", "settings.value");
            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void return_default_value_for_missing_value_if_default_is_set()
        {
            Helper.RegisterRef(() => Value, "v|value=", "VALUE", "settings.value", IPAddress.Loopback);
            
            Helper.Parse();
            Assert.AreEqual(IPAddress.Loopback, Helper.Get(() => Value));
        }

        [Test]
        public void prefer_cmd_line_before_env()
        {
            Helper.RegisterRef(() => Value, "v|value=", "VALUE", "settings.value");
            SetEnv("VALUE", "192.168.2.2");

            Helper.Parse("--value=192.168.1.1");
            Assert.AreEqual(IPAddress.Parse("192.168.1.1"), Helper.Get(() => Value));
        }

        [Test]
        public void prefer_cmd_line_before_json()
        {
            Helper.RegisterRef(() => Value, "v|value=", "VALUE", "settings.value");
            var cfg = WriteJsonConfig(new { settings = new { value = "192.168.3.3" } });
            
            Helper.Parse("-v", "192.168.1.1", "--cfg", cfg);
            Assert.AreEqual(IPAddress.Parse("192.168.1.1"), Helper.Get(() => Value));
        }

        [Test]
        public void prefer_cmd_line_before_json_and_env()
        {
            Helper.RegisterRef(() => Value, "v|value=", "VALUE", "settings.value");
            SetEnv("VALUE", "192.168.2.2");
            var cfg = WriteJsonConfig(new { settings = new { value = "192.168.3.3" } });
            
            Helper.Parse("-v:192.168.1.1", "--cfg", cfg);
            Assert.AreEqual(IPAddress.Parse("192.168.1.1"), Helper.Get(() => Value));
        }

        [Test]
        public void prefer_env_if_no_cmd_line()
        {
            Helper.RegisterRef(() => Value, "v|value=", "VALUE", "settings.value");
            SetEnv("VALUE", "192.168.2.2");
            var cfg = WriteJsonConfig(new { settings = new { value = "192.168.3.3" } });
            
            Helper.Parse("--cfg", cfg);
            Assert.AreEqual(IPAddress.Parse("192.168.2.2"), Helper.Get(() => Value));
        }

        [Test]
        public void prefer_json_if_no_cmd_line_or_env()
        {
            Helper.RegisterRef(() => Value, "v|value=", "VALUE", "settings.value");
            var cfg = WriteJsonConfig(new { settings = new { value = "192.168.3.3" } });

            Helper.Parse("--cfg", cfg);
            Assert.AreEqual(IPAddress.Parse("192.168.3.3"), Helper.Get(() => Value));
        }

        [Test]
        public void preserve_order_of_jsons()
        {
            Helper.RegisterRef(() => Value, "v|value=", "VALUE", "settings.value");
            var cfg1 = WriteJsonConfig(new { settings = new { value = "192.168.3.3" } });
            var cfg2 = WriteJsonConfig(new { settings = new { value = "192.168.4.4" } });

            Helper.Parse("--cfg", cfg1, "--cfg", cfg2);
            Assert.AreEqual(IPAddress.Parse("192.168.3.3"), Helper.Get(() => Value));
        }

        [Test]
        public void search_all_jsons_before_giving_up()
        {
            Helper.RegisterRef(() => Value, "v|value=", "VALUE", "settings.value");
            var cfg1 = WriteJsonConfig(new { settings = new { value_other = "192.168.3.3" } });
            var cfg2 = WriteJsonConfig(new { settings = new { value = "192.168.4.4" } });

            Helper.Parse("--cfg", cfg1, "--cfg", cfg2);
            Assert.AreEqual(IPAddress.Parse("192.168.4.4"), Helper.Get(() => Value));
        }

        [Test]
        public void use_default_if_all_failed()
        {
            Helper.RegisterRef(() => Value, "v|value=", "VALUE", "settings.value", IPAddress.Loopback);
            var cfg1 = WriteJsonConfig(new { settings = new { value_other = "192.168.3.3" } });
            
            Helper.Parse("--cfg", cfg1);
            Assert.AreEqual(IPAddress.Loopback, Helper.Get(() => Value));
        }
    }
}