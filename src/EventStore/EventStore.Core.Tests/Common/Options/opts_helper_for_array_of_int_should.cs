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
    public class opts_helper_for_array_of_int_should: OptsHelperTestBase
    {
        public int[] Array { get; private set; }

        [Test]
        public void parse_explicitly_present_array_from_cmd_line()
        {
            Helper.RegisterArray(() => Array, "a|arr", "settings.arr", "ARR", ",");
            
            Helper.Parse("-a", "321", "--arr=642");
            Assert.AreEqual(new [] {321, 642}, Helper.Get(() => Array));
        }

        [Test]
        public void throw_option_exception_for_missing_array_with_no_default()
        {
            Helper.RegisterArray(() => Array, "a|arr", "settings.arr", "ARR", ",");
            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void return_default_value_for_missing_value_if_default_is_set()
        {
            Helper.RegisterArray(() => Array, "a|arr", "settings.arr", "ARR", ",", new int[0]);
            
            Helper.Parse();
            Assert.AreEqual(new int[0], Helper.Get(() => Array));
        }

        [Test]
        public void prefer_cmd_line_before_env()
        {
            Helper.RegisterArray(() => Array, "a|arr", "settings.arr", "ARR", ",");
            SetEnv("VALUE", "123,246");

            Helper.Parse("--value=321", "-v:642");
            Assert.AreEqual(new[]{321, 642}, Helper.Get(() => Array));
        }

        [Test]
        public void prefer_cmd_line_before_json()
        {
            Helper.RegisterArray(() => Array, "a|arr", "settings.arr", "ARR", ",");
            var cfg = WriteJsonConfig(new { settings = new { value = new[] { 111, 222, 333 } } });
            
            Helper.Parse("-v", "321", "-v", "642", "--cfg", cfg);
            Assert.AreEqual(new[] {321, 642}, Helper.Get(() => Array));
        }

        [Test]
        public void prefer_cmd_line_before_json_and_env()
        {
            Helper.RegisterArray(() => Array, "a|arr", "settings.arr", "ARR", ",");
            SetEnv("VALUE", "123");
            var cfg = WriteJsonConfig(new { settings = new { value = new[] { 111, 222, 333 } } });
            
            Helper.Parse("-v:321", "--cfg", cfg);
            Assert.AreEqual(new[]{321}, Helper.Get(() => Array));
        }

        [Test]
        public void prefer_env_if_no_cmd_line()
        {
            Helper.RegisterArray(() => Array, "a|arr", "settings.arr", "ARR", ",");
            SetEnv("VALUE", "123,246");
            var cfg = WriteJsonConfig(new { settings = new { value = new[] { 111, 222, 333 } } });
            
            Helper.Parse("--cfg", cfg);
            Assert.AreEqual(new[] {123, 246}, Helper.Get(() => Array));
        }

        [Test]
        public void prefer_json_if_no_cmd_line_or_env()
        {
            Helper.RegisterArray(() => Array, "a|arr", "settings.arr", "ARR", ",");
            var cfg = WriteJsonConfig(new { settings = new { value = new[] { 111, 222, 333 } } });

            Helper.Parse("--cfg", cfg);
            Assert.AreEqual(new[] { 111, 222, 333 }, Helper.Get(() => Array));
        }

        [Test]
        public void preserve_order_of_jsons()
        {
            Helper.RegisterArray(() => Array, "a|arr", "settings.arr", "ARR", ",");
            var cfg1 = WriteJsonConfig(new { settings = new { value = new[] { 111, 222, 333 } } });
            var cfg2 = WriteJsonConfig(new { settings = new { value = new[] { 444, 555, 666 } } });

            Helper.Parse("--cfg", cfg1, "--cfg", cfg2);
            Assert.AreEqual(new[] { 111, 222, 333 }, Helper.Get(() => Array));
        }

        [Test]
        public void search_all_jsons_before_giving_up()
        {
            Helper.RegisterArray(() => Array, "a|arr", "settings.arr", "ARR", ",");
            var cfg1 = WriteJsonConfig(new { settings = new { value_other = new[] { 111, 222, 333 } } });
            var cfg2 = WriteJsonConfig(new { settings = new { value = new[] { 444, 555, 666 } } });

            Helper.Parse("--cfg", cfg1, "--cfg", cfg2);
            Assert.AreEqual(new[] { 444, 555, 666 }, Helper.Get(() => Array));
        }

        [Test]
        public void use_default_if_all_failed()
        {
            Helper.RegisterArray(() => Array, "a|arr", "settings.arr", "ARR", ",", new[] { 777 });
            var cfg1 = WriteJsonConfig(new { settings = new { value_other = false } });
            
            Helper.Parse("--cfg", cfg1);
            Assert.AreEqual(new[] { 777 }, Helper.Get(() => Array));
        }
    }
}