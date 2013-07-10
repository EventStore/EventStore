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
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Used to build a cluster settings (fluent API)
    /// </summary>
    public class ClusterSettingsBuilder
    {
        private string _clusterDns;
        private int _maxDiscoverAttempts = Consts.DefaultMaxClusterDiscoverAttempts;
        private int _managerExternalHttpPort = Consts.DefaultClusterManagerExternalHttpPort;

        private IPAddress[] _fakeDnsEntries;
        private TimeSpan _gossipTimeout = TimeSpan.FromSeconds(1);

        internal ClusterSettingsBuilder()
        {
        }

        public ClusterSettingsBuilder SetClusterDns(string clusterDns)
        {
            Ensure.NotNullOrEmpty(clusterDns, "clusterDns");
            _clusterDns = clusterDns;
            return this;
        }

        public ClusterSettingsBuilder SetMaxDiscoverAttempts(int maxDiscoverAttempts)
        {
            if (maxDiscoverAttempts < -1)
                throw new ArgumentOutOfRangeException("maxDiscoverAttempts", string.Format("maxDiscoverAttempts value is out of range: {0}. Allowed range: [-1, infinity].", maxDiscoverAttempts));
            _maxDiscoverAttempts = maxDiscoverAttempts;
            return this;
        }

        public ClusterSettingsBuilder WithGossipTimeoutOf(TimeSpan timeout)
        {
            _gossipTimeout = timeout;
            return this;
        }

        public ClusterSettingsBuilder SetManagerExternalHttpPort(int managerExternalHttpPort)
        {
            Ensure.Positive(managerExternalHttpPort, "managerExternalHttpPort");
            _managerExternalHttpPort = managerExternalHttpPort;
            return this;
        }

        public ClusterSettingsBuilder SetFakeDnsEntries(params IPAddress[] dnsEntries)
        {
            if (dnsEntries == null || dnsEntries.Length == 0)
                throw new ArgumentException("Empty FakeDnsEntries collection.");
            _fakeDnsEntries = dnsEntries;
            return this;
        }

        public static implicit operator ClusterSettings(ClusterSettingsBuilder builder)
        {
            return new ClusterSettings(builder._clusterDns,
                                       builder._maxDiscoverAttempts,
                                       builder._managerExternalHttpPort,
                                       builder._fakeDnsEntries,
                                       builder._gossipTimeout);
        }
    }
}