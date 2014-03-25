using System;
using EventStore.Common.Options;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.Options
{
    [TestFixture]
    public class opts_helper_for_array_of_int_should: OptsHelperTestBase
    {
        public int[] Array { get { throw new InvalidOperationException(); } }

        [Test]
        public void parse_explicitly_present_array_from_cmd_line()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);
            
            Helper.Parse("-a", "321", "--arr", "642");
            Assert.AreEqual(new [] {321, 642}, Helper.Get(() => Array));
        }

        [Test]
        public void throw_option_exception_for_missing_array_with_no_default()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);
            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void return_default_value_for_missing_value_if_default_is_set()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", new int[0]);
            
            Helper.Parse();
            Assert.AreEqual(new int[0], Helper.Get(() => Array));
        }

        [Test]
        public void prefer_cmd_line_before_env()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);
            SetEnv("ARR", "123,246");

            Helper.Parse("--arr=321", "-a:642");
            Assert.AreEqual(new[]{321, 642}, Helper.Get(() => Array));
        }

        [Test]
        public void prefer_cmd_line_before_json()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);
            var cfg = WriteJsonConfig(new { settings = new { arr = new[] { 111, 222, 333 } } });
            
            Helper.Parse("-a", "321", "-a", "642", "--cfg", cfg);
            Assert.AreEqual(new[] {321, 642}, Helper.Get(() => Array));
        }

        [Test]
        public void prefer_cmd_line_before_json_and_env()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);
            SetEnv("ARR", "123");
            var cfg = WriteJsonConfig(new { settings = new { arr = new[] { 111, 222, 333 } } });
            
            Helper.Parse("-a:321", "--cfg", cfg);
            Assert.AreEqual(new[]{321}, Helper.Get(() => Array));
        }

        [Test]
        public void prefer_env_if_no_cmd_line()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);
            SetEnv("ARR", "123,246");
            var cfg = WriteJsonConfig(new { settings = new { arr = new[] { 111, 222, 333 } } });
            
            Helper.Parse("--cfg", cfg);
            Assert.AreEqual(new[] {123, 246}, Helper.Get(() => Array));
        }

        [Test]
        public void prefer_json_if_no_cmd_line_or_env()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);
            var cfg = WriteJsonConfig(new { settings = new { arr = new[] { 111, 222, 333 } } });

            Helper.Parse("--cfg", cfg);
            Assert.AreEqual(new[] { 111, 222, 333 }, Helper.Get(() => Array));
        }

        [Test]
        public void preserve_order_of_jsons()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);
            var cfg1 = WriteJsonConfig(new { settings = new { arr = new[] { 111, 222, 333 } } });
            var cfg2 = WriteJsonConfig(new { settings = new { arr = new[] { 444, 555, 666 } } });

            Helper.Parse("--cfg", cfg1, "--cfg", cfg2);
            Assert.AreEqual(new[] { 111, 222, 333 }, Helper.Get(() => Array));
        }

        [Test]
        public void search_all_jsons_before_giving_up()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", null);
            var cfg1 = WriteJsonConfig(new { settings = new { arr_other = new[] { 111, 222, 333 } } });
            var cfg2 = WriteJsonConfig(new { settings = new { arr = new[] { 444, 555, 666 } } });

            Helper.Parse("--cfg", cfg1, "--cfg", cfg2);
            Assert.AreEqual(new[] { 444, 555, 666 }, Helper.Get(() => Array));
        }

        [Test]
        public void use_default_if_all_failed()
        {
            Helper.RegisterArray<int>(() => Array, "a|arr=", "ARR", ",", "settings.arr", new[] { 777 });
            var cfg1 = WriteJsonConfig(new { settings = new { arr_other = false } });
            
            Helper.Parse("--cfg", cfg1);
            Assert.AreEqual(new[] { 777 }, Helper.Get(() => Array));
        }
    }
}