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
using System.Collections.Generic;
using System.Net;
using EventStore.Common.CommandLine.lib;

namespace EventStore.Common.CommandLine
{
    public abstract class EventStoreCmdLineOptionsBase : CommandLineOptionsBase
    {
        [Option(null, "ip", Required = true)]
        public IPAddress Ip { get; set; }

        [Option("h", "http-port", Required = true)]
        public virtual int HttpPort { get; set; }

        [Option("s", "stats-period-sec", DefaultValue = 30)]
        public int StatsPeriodSec { get; set; }

        [Option("c", "chunkcache", DefaultValue = 2)]
        public int ChunksToCache { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }

        public override IEnumerable<KeyValuePair<string, string>> GetLoadedOptionsPairs()
        {
            yield return new KeyValuePair<string, string>("IP", Ip.ToString());
            yield return new KeyValuePair<string, string>("HTTP PORT", HttpPort.ToString());
            yield return new KeyValuePair<string, string>("STATS PERIOD SEC", StatsPeriodSec.ToString());
        }
    }
}