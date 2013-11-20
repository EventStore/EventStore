﻿// Copyright (c) 2012, Event Store LLP
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