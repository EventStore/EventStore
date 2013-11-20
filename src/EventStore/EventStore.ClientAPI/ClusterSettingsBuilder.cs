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
    /// Builder used for creating instances of ClusterSettings.
    /// </summary>
    public class ClusterSettingsBuilder
    {
        private string _clusterDns;
        private int _maxDiscoverAttempts = Consts.DefaultMaxClusterDiscoverAttempts;
        private int _managerExternalHttpPort = Consts.DefaultClusterManagerExternalHttpPort;

        private IPEndPoint[] _gossipSeeds;
        private TimeSpan _gossipTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Sets the DNS name under which cluster nodes are listed.
        /// </summary>
        /// <param name="clusterDns">The DNS name under which cluster nodes are listed.</param>
        /// <returns>A <see cref="ClusterSettingsBuilder"/> for further configuration.</returns>
        public ClusterSettingsBuilder SetClusterDns(string clusterDns)
        {
            Ensure.NotNullOrEmpty(clusterDns, "clusterDns");
            _clusterDns = clusterDns;
            return this;
        }

        /// <summary>
        /// Sets the maximum number of attempts for DNS discovery.
        /// </summary>
        /// <param name="maxDiscoverAttempts">The maximum number of attempts for DNS discovery.</param>
        /// <returns>A <see cref="ClusterSettingsBuilder"/> for further configuration.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If maxDiscoverAttempts is less than 0.</exception>
        public ClusterSettingsBuilder SetMaxDiscoverAttempts(int maxDiscoverAttempts)
        {
            if (maxDiscoverAttempts < -1)
                throw new ArgumentOutOfRangeException("maxDiscoverAttempts", string.Format("maxDiscoverAttempts value is out of range: {0}. Allowed range: [-1, infinity].", maxDiscoverAttempts));
            _maxDiscoverAttempts = maxDiscoverAttempts;
            return this;
        }

        /// <summary>
        /// Sets the period after which gossip times out if none is received.
        /// </summary>
        /// <param name="timeout">The period after which gossip times out if none is received.</param>
        /// <returns>A <see cref="ClusterSettingsBuilder"/> for further configuration.</returns>
        public ClusterSettingsBuilder WithGossipTimeoutOf(TimeSpan timeout)
        {
            _gossipTimeout = timeout;
            return this;
        }

        /// <summary>
        /// Sets the port with which the client will communicate with cluster managers in
        /// commercial versions of the Event Store. This should be the external HTTP port
        /// of the manager.
        /// </summary>
        /// <param name="managerExternalHttpPort">The external HTTP port of cluster managers.</param>
        /// <returns>A <see cref="ClusterSettingsBuilder"/> for further configuration.</returns>
        public ClusterSettingsBuilder SetManagerExternalHttpPort(int managerExternalHttpPort)
        {
            Ensure.Positive(managerExternalHttpPort, "managerExternalHttpPort");
            _managerExternalHttpPort = managerExternalHttpPort;
            return this;
        }

        /// <summary>
        /// Sets the port on which cluster members gossip.
        /// </summary>
        /// <param name="gossipPort">The port on which cluster members gossip.</param>
        /// <returns>A <see cref="ClusterSettingsBuilder"/> for further configuration.</returns>
        public ClusterSettingsBuilder SetGossipPort(int gossipPort)
        {
            Ensure.Positive(gossipPort, "gossipPort");
            _managerExternalHttpPort = gossipPort;
            return this;
        }

        /// <summary>
        /// Sets gossip seed endpoints for the client.
        /// </summary>
        /// <param name="gossipSeeds">IPEndPoints representing the endpoints of nodes from which to seed gossip.</param>
        /// <returns>A <see cref="ClusterSettingsBuilder"/> for further configuration.</returns>
        /// <exception cref="ArgumentException">If no gossip seeds are specified.</exception>
        public ClusterSettingsBuilder WithGossipSeeds(params IPEndPoint[] gossipSeeds)
        {
            if (gossipSeeds == null || gossipSeeds.Length == 0)
                throw new ArgumentException("Empty FakeDnsEntries collection.");
            _gossipSeeds = gossipSeeds;
            return this;
        }

        /// <summary>
        /// Builds a <see cref="ClusterSettings"/> object from a <see cref="ClusterSettingsBuilder"/>.
        /// </summary>
        /// <param name="builder"><see cref="ClusterSettingsBuilder"/> from which to build a <see cref="ClusterSettings"/></param>
        /// <returns></returns>
        public static implicit operator ClusterSettings(ClusterSettingsBuilder builder)
        {
            return new ClusterSettings(builder._clusterDns,
                                       builder._maxDiscoverAttempts,
                                       builder._managerExternalHttpPort,
                                       builder._gossipSeeds,
                                       builder._gossipTimeout);
        }
    }
}