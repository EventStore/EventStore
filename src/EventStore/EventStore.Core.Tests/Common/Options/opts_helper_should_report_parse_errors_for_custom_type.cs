using System;
using System.Net;
using EventStore.Common.Options;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.Options
{
    [TestFixture]
    public class opts_helper_should_report_parse_errors_for_custom_type : OptsHelperTestBase
    {
        public IPAddress Ip { get { throw new InvalidOperationException(); } }

        [Test]
        public void with_no_value_in_cmd_line()
        {
            Helper.RegisterRef(() => Ip, "i|ip=", "IP", "settings.ip");

            Assert.Throws<OptionException>(() => Helper.Parse("-i"));
        }

        [Test]
        public void if_value_is_provided_more_than_once()
        {
            Helper.RegisterRef(() => Ip, "i|ip=", "IP", "settings.ip");

            Assert.Throws<OptionException>(() => Helper.Parse("-i", "192.168.1.1", "--ip", "192.168.1.1"));
        }

        [Test]
        public void with_wrong_format_in_cmd_line()
        {
            Helper.RegisterRef(() => Ip, "i|ip=", "IP", "settings.ip");

            Assert.Throws<OptionException>(() => Helper.Parse("-i", "127.0..1"));
        }

        [Test]
        public void with_wrong_format_in_env()
        {
            Helper.RegisterRef(() => Ip, "i|ip=", "IP", "settings.ip");
            SetEnv("IP", "127,0,0,1");

            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void with_wrong_type_in_json()
        {
            Helper.RegisterRef(() => Ip, "i|ip=", "IP", "settings.ip");
            var cfg = WriteJsonConfig(new { settings = new { ip = new { } } });

            Assert.Throws<OptionException>(() => Helper.Parse("--cfg", cfg));
        }

        [Test]
        public void with_wrong_format_in_json()
        {
            Helper.RegisterRef(() => Ip, "i|ip=", "IP", "settings.ip");
            var cfg = WriteJsonConfig(new { settings = new { ip = "127:1:1:1" } });

            Assert.Throws<OptionException>(() => Helper.Parse("--cfg", cfg));
        }
    }
}