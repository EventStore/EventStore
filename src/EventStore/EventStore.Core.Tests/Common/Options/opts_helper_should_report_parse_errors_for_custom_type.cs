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
    public class opts_helper_should_report_parse_errors_for_custom_type : OptsHelperTestBase
    {
        public IPAddress Ip { get { throw new InvalidOperationException(); } }

        [Test]
        public void with_no_value_in_cmd_line()
        {
            Helper.RegisterRef(() => Ip, "i|ip=", "settings.ip", "IP");

            Assert.Throws<OptionException>(() => Helper.Parse("-i"));
        }

        [Test]
        public void if_value_is_provided_more_than_once()
        {
            Helper.RegisterRef(() => Ip, "i|ip=", "settings.ip", "IP");

            Assert.Throws<OptionException>(() => Helper.Parse("-i", "192.168.1.1", "--ip", "192.168.1.1"));
        }

        [Test]
        public void with_wrong_format_in_cmd_line()
        {
            Helper.RegisterRef(() => Ip, "i|ip=", "settings.ip", "IP");

            Assert.Throws<OptionException>(() => Helper.Parse("-i", "127.0..1"));
        }

        [Test]
        public void with_wrong_format_in_env()
        {
            Helper.RegisterRef(() => Ip, "i|ip=", "settings.ip", "IP");
            SetEnv("IP", "127,0,0,1");

            Assert.Throws<OptionException>(() => Helper.Parse());
        }

        [Test]
        public void with_wrong_type_in_json()
        {
            Helper.RegisterRef(() => Ip, "i|ip=", "settings.ip", "IP");
            var cfg = WriteJsonConfig(new { settings = new { ip = new { } } });

            Assert.Throws<OptionException>(() => Helper.Parse("--cfg", cfg));
        }

        [Test]
        public void with_wrong_format_in_json()
        {
            Helper.RegisterRef(() => Ip, "i|ip=", "settings.ip", "IP");
            var cfg = WriteJsonConfig(new { settings = new { ip = "127:1:1:1" } });

            Assert.Throws<OptionException>(() => Helper.Parse("--cfg", cfg));
        }
    }
}