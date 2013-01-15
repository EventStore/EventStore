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
using Mono.Options;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.Options
{
    [TestFixture]
    public class opts_helper_for_bool_flag_should: OptsHelperTestBase
    {
        public bool Flag { get; private set; }

        [Test]
        public void parse_explicitly_present_bool_from_cmd_line()
        {
            Helper.Register(() => Flag, "f|flag", "settings.flag", "FLAG");
            
            Helper.Parse("-f");
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void parse_explicitly_present_negative_flag_from_cmd_line()
        {
            Helper.Register(() => Flag, "f|flag", "settings.flag", "FLAG");
            
            Helper.Parse("-f-");
            Assert.IsFalse(Helper.Get(() => Flag));
        }

        [Test]
        public void throw_option_exception_for_missing_flag_with_no_default()
        {
            Helper.Register(() => Flag, "f|flag", "settings.flag", "FLAG");
            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void return_default_value_for_missing_flag_if_default_is_set()
        {
            Helper.Register(() => Flag, "f|flag", "settings.flag", "FLAG", true);
            
            Helper.Parse();
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void prefer_cmd_line_before_env()
        {
            Helper.Register(() => Flag, "f|flag", "settings.flag", "FLAG");
            SetEnv("FLAG", "0");

            Helper.Parse("-f");
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void prefer_cmd_line_before_json()
        {
            Helper.Register(() => Flag, "f|flag", "settings.flag", "FLAG");
            var cfg = WriteJsonConfig(new { settings = new { flag = false } });
            
            Helper.Parse("-f", "--cfg", cfg);
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void prefer_cmd_line_before_json_and_env()
        {
            Helper.Register(() => Flag, "f|flag", "settings.flag", "FLAG");
            SetEnv("FLAG", "0");
            var cfg = WriteJsonConfig(new { settings = new { flag = false } });
            
            Helper.Parse("-f", "--cfg", cfg);
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void prefer_env_if_no_cmd_line()
        {
            Helper.Register(() => Flag, "f|flag", "settings.flag", "FLAG");
            SetEnv("FLAG", "1");
            var cfg = WriteJsonConfig(new { settings = new { flag = false } });
            
            Helper.Parse("--cfg", cfg);
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void prefer_json_if_no_cmd_line_or_env()
        {
            Helper.Register(() => Flag, "f|flag", "settings.flag", "FLAG");
            var cfg = WriteJsonConfig(new { settings = new { flag = true } });

            Helper.Parse("--cfg", cfg);
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void preserve_order_of_jsons()
        {
            Helper.Register(() => Flag, "f|flag", "settings.flag", "FLAG");
            var cfg1 = WriteJsonConfig(new { settings = new { flag = false } });
            var cfg2 = WriteJsonConfig(new { settings = new { flag = true } });

            Helper.Parse("--cfg", cfg1, "--cfg", cfg2);
            Assert.IsFalse(Helper.Get(() => Flag));
        }

        [Test]
        public void search_all_jsons_before_giving_up()
        {
            Helper.Register(() => Flag, "f|flag", "settings.flag", "FLAG");
            var cfg1 = WriteJsonConfig(new { settings = new { flag_other = false } });
            var cfg2 = WriteJsonConfig(new { settings = new { flag = true } });

            Helper.Parse("--cfg", cfg1, "--cfg", cfg2);
            Assert.IsTrue(Helper.Get(() => Flag));
        }

        [Test]
        public void use_default_if_all_failed()
        {
            Helper.Register(() => Flag, "f|flag", "settings.flag", "FLAG", true);
            var cfg1 = WriteJsonConfig(new { settings = new { flag_other = false } });
            
            Helper.Parse("--cfg", cfg1);
            Assert.IsTrue(Helper.Get(() => Flag));
        }
    }
}