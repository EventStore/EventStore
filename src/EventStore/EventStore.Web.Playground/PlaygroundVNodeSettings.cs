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

using System.Net;
using EventStore.Common.Utils;

namespace EventStore.Web.Playground
{
    public class PlaygroundVNodeSettings
    {
        public readonly IPEndPoint ExternalTcpEndPoint;
        public readonly IPEndPoint ExternalHttpEndPoint;
        public readonly string[] HttpPrefixes;
        public readonly int HttpSendingThreads;
        public readonly int HttpReceivingThreads;
        public readonly int TcpSendingThreads;


        public PlaygroundVNodeSettings(
            IPEndPoint externalTcpEndPoint, IPEndPoint externalHttpEndPoint, string[] httpPrefixes,
            int httpSendingThreads, int httpReceivingThreads, int tcpSendingThreads)
        {
            Ensure.NotNull(externalTcpEndPoint, "externalTcpEndPoint");
            Ensure.NotNull(externalHttpEndPoint, "externalHttpEndPoint");
            Ensure.NotNull(httpPrefixes, "httpPrefixes");
            Ensure.Positive(httpReceivingThreads, "httpReceivingThreads");
            Ensure.Positive(httpSendingThreads, "httpSendingThreads");
            Ensure.Positive(tcpSendingThreads, "tcpSendingThreads");

            ExternalTcpEndPoint = externalTcpEndPoint;
            ExternalHttpEndPoint = externalHttpEndPoint;
            HttpPrefixes = httpPrefixes;
            HttpSendingThreads = httpSendingThreads;
            HttpReceivingThreads = httpReceivingThreads;
            TcpSendingThreads = tcpSendingThreads;
        }

        public override string ToString()
        {
            return
                string.Format(
                    "ExternalTcpEndPoint: {0},\n" + "ExternalHttpEndPoint: {1},\n" + "HttpPrefixes: {2},\n"
                    + "HttpSendingThreads: {3}\n" + "HttpReceivingThreads: {4}\n" + "TcpSendingThreads: {5}\n",
                    ExternalTcpEndPoint, ExternalHttpEndPoint, string.Join(", ", HttpPrefixes), HttpSendingThreads,
                    HttpReceivingThreads, TcpSendingThreads);
        }
    }
}
