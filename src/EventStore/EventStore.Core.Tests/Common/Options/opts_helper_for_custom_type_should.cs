// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Net;
using Mono.Options;
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
            Helper.RegisterRef(() => Value, "v|value=", "settings.value", "VALUE");
            
            Helper.Parse("-v", "192.168.1.1");
            Assert.AreEqual(IPAddress.Parse("192.168.1.1"), Helper.Get(() => Value));
        }

        [Test]
        public void throw_option_exception_for_missing_value_with_no_default()
        {
            Helper.RegisterRef(() => Value, "v|value=", "settings.value", "VALUE");
            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void return_default_value_for_missing_value_if_default_is_set()
        {
            Helper.RegisterRef(() => Value, "v|value=", "settings.value", "VALUE", IPAddress.Loopback);
            
            Helper.Parse();
            Assert.AreEqual(IPAddress.Loopback, Helper.Get(() => Value));
        }

        [Test]
        public void prefer_cmd_line_before_env()
        {
            Helper.RegisterRef(() => Value, "v|value=", "settings.value", "VALUE");
            SetEnv("VALUE", "192.168.2.2");

            Helper.Parse("--value=192.168.1.1");
            Assert.AreEqual(IPAddress.Parse("192.168.1.1"), Helper.Get(() => Value));
        }

        [Test]
        public void prefer_cmd_line_before_json()
        {
            Helper.RegisterRef(() => Value, "v|value=", "settings.value", "VALUE");
            var cfg = WriteJsonConfig(new { settings = new { value = "192.168.3.3" } });
            
            Helper.Parse("-v", "192.168.1.1", "--cfg", cfg);
            Assert.AreEqual(IPAddress.Parse("192.168.1.1"), Helper.Get(() => Value));
        }

        [Test]
        public void prefer_cmd_line_before_json_and_env()
        {
            Helper.RegisterRef(() => Value, "v|value=", "settings.value", "VALUE");
            SetEnv("VALUE", "192.168.2.2");
            var cfg = WriteJsonConfig(new { settings = new { value = "192.168.3.3" } });
            
            Helper.Parse("-v:192.168.1.1", "--cfg", cfg);
            Assert.AreEqual(IPAddress.Parse("192.168.1.1"), Helper.Get(() => Value));
        }

        [Test]
        public void prefer_env_if_no_cmd_line()
        {
            Helper.RegisterRef(() => Value, "v|value=", "settings.value", "VALUE");
            SetEnv("VALUE", "192.168.2.2");
            var cfg = WriteJsonConfig(new { settings = new { value = "192.168.3.3" } });
            
            Helper.Parse("--cfg", cfg);
            Assert.AreEqual(IPAddress.Parse("192.168.2.2"), Helper.Get(() => Value));
        }

        [Test]
        public void prefer_json_if_no_cmd_line_or_env()
        {
            Helper.RegisterRef(() => Value, "v|value=", "settings.value", "VALUE");
            var cfg = WriteJsonConfig(new { settings = new { value = "192.168.3.3" } });

            Helper.Parse("--cfg", cfg);
            Assert.AreEqual(IPAddress.Parse("192.168.3.3"), Helper.Get(() => Value));
        }

        [Test]
        public void preserve_order_of_jsons()
        {
            Helper.RegisterRef(() => Value, "v|value=", "settings.value", "VALUE");
            var cfg1 = WriteJsonConfig(new { settings = new { value = "192.168.3.3" } });
            var cfg2 = WriteJsonConfig(new { settings = new { value = "192.168.4.4" } });

            Helper.Parse("--cfg", cfg1, "--cfg", cfg2);
            Assert.AreEqual(IPAddress.Parse("192.168.3.3"), Helper.Get(() => Value));
        }

        [Test]
        public void search_all_jsons_before_giving_up()
        {
            Helper.RegisterRef(() => Value, "v|value=", "settings.value", "VALUE");
            var cfg1 = WriteJsonConfig(new { settings = new { value_other = "192.168.3.3" } });
            var cfg2 = WriteJsonConfig(new { settings = new { value = "192.168.4.4" } });

            Helper.Parse("--cfg", cfg1, "--cfg", cfg2);
            Assert.AreEqual(IPAddress.Parse("192.168.4.4"), Helper.Get(() => Value));
        }

        [Test]
        public void use_default_if_all_failed()
        {
            Helper.RegisterRef(() => Value, "v|value=", "settings.value", "VALUE", IPAddress.Loopback);
            var cfg1 = WriteJsonConfig(new { settings = new { value_other = "192.168.3.3" } });
            
            Helper.Parse("--cfg", cfg1);
            Assert.AreEqual(IPAddress.Loopback, Helper.Get(() => Value));
        }
    }
}