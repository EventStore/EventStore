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
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;
using EventStore.Core.Services.Monitoring;

namespace EventStore.Core.Settings
{
    public class SingleVNodeSettings
    {
        public readonly IPEndPoint ExternalTcpEndPoint;
        public readonly IPEndPoint ExternalSecureTcpEndPoint;
        public readonly IPEndPoint ExternalHttpEndPoint;
        public readonly string[] HttpPrefixes;
        public readonly bool EnableTrustedAuth;
        public readonly X509Certificate2 Certificate;
        public readonly int WorkerThreads;

        public readonly TimeSpan MinFlushDelay;
        public readonly TimeSpan PrepareTimeout;
        public readonly TimeSpan CommitTimeout;

        public readonly bool DisableScavengeMerging;

        public readonly TimeSpan StatsPeriod;
        public readonly StatsStorage StatsStorage;

        public readonly bool SkipInitializeStandardUsersCheck;

        public SingleVNodeSettings(IPEndPoint externalTcpEndPoint, 
                                   IPEndPoint externalSecureTcpEndPoint,
                                   IPEndPoint externalHttpEndPoint, 
                                   string[] httpPrefixes,
                                   bool enableTrustedAuth,
                                   X509Certificate2 certificate,
                                   int workerThreads, 
                                   TimeSpan minFlushDelay,
                                   TimeSpan prepareTimeout,
                                   TimeSpan commitTimeout,
                                   TimeSpan statsPeriod, 
                                   StatsStorage statsStorage = StatsStorage.StreamAndCsv,
                                   bool skipInitializeStandardUsersCheck = false,
                                   bool disableScavengeMerging = false)
        {
            Ensure.NotNull(externalTcpEndPoint, "externalTcpEndPoint");
            Ensure.NotNull(externalHttpEndPoint, "externalHttpEndPoint");
            Ensure.NotNull(httpPrefixes, "httpPrefixes");
            if (externalSecureTcpEndPoint != null)
                Ensure.NotNull(certificate, "certificate");
            Ensure.Positive(workerThreads, "workerThreads");

            ExternalTcpEndPoint = externalTcpEndPoint;
            ExternalSecureTcpEndPoint = externalSecureTcpEndPoint;
            ExternalHttpEndPoint = externalHttpEndPoint;
            HttpPrefixes = httpPrefixes;
            EnableTrustedAuth = enableTrustedAuth;
            Certificate = certificate;
            WorkerThreads = workerThreads;

            MinFlushDelay = minFlushDelay;
            PrepareTimeout = prepareTimeout;
            CommitTimeout = commitTimeout;

            StatsPeriod = statsPeriod;
            StatsStorage = statsStorage;

            SkipInitializeStandardUsersCheck = skipInitializeStandardUsersCheck;
            DisableScavengeMerging = disableScavengeMerging;
        }

        public override string ToString()
        {
            return string.Format("ExternalTcpEndPoint: {0},\n"
                                 + "ExternalSecureTcpEndPoint: {1},\n"
                                 + "ExternalHttpEndPoint: {2},\n"
                                 + "HttpPrefixes: {3},\n"
                                 + "EnableTrustedAuth: {4},\n"
                                 + "Certificate: {5},\n"
                                 + "WorkerThreads: {6}\n" 
                                 + "MinFlushDelay: {7}\n"
                                 + "PrepareTimeout: {8}\n"
                                 + "CommitTimeout: {9}\n"
                                 + "StatsPeriod: {10}\n"
                                 + "StatsStorage: {11}",
                                 ExternalTcpEndPoint,
                                 ExternalSecureTcpEndPoint == null ? "n/a" : ExternalSecureTcpEndPoint.ToString(),
                                 ExternalHttpEndPoint,
                                 string.Join(", ", HttpPrefixes),
                                 EnableTrustedAuth,
                                 Certificate == null ? "n/a" : Certificate.ToString(verbose: true),
                                 WorkerThreads,
                                 MinFlushDelay,
                                 PrepareTimeout,
                                 CommitTimeout,
                                 StatsPeriod,
                                 StatsStorage);
        }
    }


}