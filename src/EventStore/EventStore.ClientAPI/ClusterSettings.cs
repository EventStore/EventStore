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
    public sealed class ClusterSettings
    {
        /// <summary>
        /// Creates a new set of <see cref="ClusterSettings"/>
        /// </summary>
        /// <returns>A <see cref="ClusterSettingsBuilder"/> that can be used to build up a <see cref="ClusterSettings"/></returns>
        public static ClusterSettingsBuilder Create()
        {
            return new ClusterSettingsBuilder();
        }

        public readonly string ClusterDns;
        public readonly int MaxDiscoverAttempts;
        public readonly int ManagerExternalHttpPort;

        public readonly IPEndPoint[] GossipSeeds;
        public TimeSpan GossipTimeout;

        internal ClusterSettings(string clusterDns, int maxDiscoverAttempts, int managerExternalHttpPort, IPEndPoint[] gossipSeeds, TimeSpan gossipTimeout)
        {
            Ensure.NotNullOrEmpty(clusterDns, "clusterDns");
            if (maxDiscoverAttempts < -1)
                throw new ArgumentOutOfRangeException("maxDiscoverAttempts", string.Format("maxDiscoverAttempts value is out of range: {0}. Allowed range: [-1, infinity].", maxDiscoverAttempts));
            Ensure.Positive(managerExternalHttpPort, "managerExternalHttpPort");

            ClusterDns = clusterDns;
            MaxDiscoverAttempts = maxDiscoverAttempts;
            ManagerExternalHttpPort = managerExternalHttpPort;
            GossipTimeout = gossipTimeout;
            GossipSeeds = gossipSeeds;
        }
    }
}
