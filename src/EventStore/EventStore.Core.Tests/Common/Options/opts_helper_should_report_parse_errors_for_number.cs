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
using EventStore.Common.Options;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.Options
{
    [TestFixture]
    public class opts_helper_should_report_parse_errors_for_number: OptsHelperTestBase
    {
        public int Number { get { throw new InvalidOperationException(); } }

        [Test]
        public void with_no_value_in_cmd_line()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");

            Assert.Throws<OptionException>(() => Helper.Parse("-n"));
        }

        [Test]
        public void with_non_numeric_value_in_cmd_line()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");

            Assert.Throws<OptionException>(() => Helper.Parse("-n", "not-a-number"));
        }

        [Test]
        public void with_overflow_in_cmd_line()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");

            Assert.Throws<OptionException>(() => Helper.Parse("-n", "123123123123123123"));
        }

        [Test]
        public void with_floating_point_in_cmd_line()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");

            Assert.Throws<OptionException>(() => Helper.Parse("-n", "123.123"));
        }

        [Test]
        public void with_non_numeric_value_in_env()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");
            SetEnv("NUM", "ABC");

            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void with_overflow_in_env()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");
            SetEnv("NUM", "123123123123123123");
            
            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void with_floating_point_in_env()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");
            SetEnv("NUM", "123.123");

            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void with_no_value_in_json()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");
            var cfg = WriteJsonConfig(new {settings = new {num = new {}}});
            
            Assert.Throws<OptionException>(() => Helper.Parse("--cfg", cfg));
        }

        [Test]
        public void with_non_numeric_value_in_json()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");
            var cfg = WriteJsonConfig(new { settings = new { num = "abc" } });

            Assert.Throws<OptionException>(() => Helper.Parse("--cfg", cfg));
        }

        [Test]
        public void with_overflow_in_json()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");
            var cfg = WriteJsonConfig(new { settings = new { num = 123123123123123L } });

            Assert.Throws<OptionException>(() => Helper.Parse("--cfg", cfg));
        }

        [Test, Ignore("If this is really needed, we have to make additional checks, for now I'm leaving it with default automatic conversion.")]
        public void with_floating_point_in_json()
        {
            Helper.Register(() => Number, "n|num=", "NUM", "settings.num");
            var cfg = WriteJsonConfig(new { settings = new { num = 123.123 } });

            Helper.Parse("--cfg", cfg);
            Assert.Fail();
        }
    }
}