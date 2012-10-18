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
using System.Collections.Generic;
using EventStore.Common.CommandLine;
using EventStore.Common.CommandLine.lib;

namespace EventStore.TestClient
{
    /// <summary>
    /// Data contract for the command-line options accepted by test client.
    /// This contract is handled by CommandLine project for .NET
    /// </summary>
    public sealed class ClientOptions : EventStoreCmdLineOptionsBase
    {
        [Option("i", "ip", DefaultValue = "127.0.0.1", HelpText = "IP address of server")]
        public string Ip { get; set; }

        [Option("t", "tcp-port", DefaultValue = 1113, HelpText = "TCP port on server")]
        public int TcpPort { get; set; }

        [Option("h", "http-port", DefaultValue = 2113, HelpText = "HTPP port on server")]
        public int HttpPort { get; set; }

        [Option(null, "timeout", DefaultValue = -1, HelpText = "Timeout for command execution in seconds, -1 for infinity")]
        public int Timeout { get; set; }

        [Option("r", "read-window", DefaultValue = 50, HelpText = "The difference between sent/received read commands")]
        public int ReadWindow { get; set; }

        [Option("w", "write-window", DefaultValue = 50, HelpText = "The difference between sent/received write commands")]
        public int WriteWindow { get; set; }

        [Option("p", "ping-window", DefaultValue = 50, HelpText = "The difference between sent/received ping commands")]
        public int PingWindow { get; set; }

        [ValueList(typeof(List<string>), MaximumElements = -1)]
        public IList<string> Command { get; set; }

        [HelpOption]
        public override string GetUsage()
        {
            var help = new HelpText
            {
                Heading = "EventStore.Client",
                AddDashesToOption = true
            };

            HandleParsingErrorsInHelp(help);
            help.AddPreOptionsLine("Usage: EventStore.Client -i 127.0.0.1 -t 1113 -h 2113");
            help.AddOptions(this);

            return help;
        }

        private void HandleParsingErrorsInHelp(HelpText help)
        {
            if (this.LastPostParsingState.Errors.Count > 0)
            {
                var errors = help.RenderParsingErrorsText(this, 2); // indent with two spaces
                if (!string.IsNullOrEmpty(errors))
                {
                    help.AddPreOptionsLine(string.Concat(Environment.NewLine, "ERROR(S):"));
                    help.AddPreOptionsLine(errors);
                }
            }
        }

        public override IEnumerable<KeyValuePair<string, string>> GetLoadedOptionsPairs()
        {
            foreach (var pair in base.GetLoadedOptionsPairs())
                yield return pair;
            yield return new KeyValuePair<string, string>("IP", Ip);
            yield return new KeyValuePair<string, string>("TCP PORT", TcpPort.ToString());
            yield return new KeyValuePair<string, string>("HTTP PORT", HttpPort.ToString());
            yield return new KeyValuePair<string, string>("TIMEOUT", Timeout.ToString());
            yield return new KeyValuePair<string, string>("READ WINDOW", ReadWindow.ToString());
            yield return new KeyValuePair<string, string>("WRITE WINDOW", WriteWindow.ToString());
            yield return new KeyValuePair<string, string>("PING WINDOW", PingWindow.ToString());
        }
    }
}