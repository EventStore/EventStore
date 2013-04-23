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
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI
{
    public static class EventStoreConnection
    {
        /// <summary>
        /// Creates a new <see cref="EventStoreConnection"/> using default <see cref="ConnectionSettings"/>
        /// </summary>
        /// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
        /// <param name="tcpEndPoint">The <see cref="IPEndPoint"/> to connect to.</param>
        /// <returns>a new <see cref="EventStoreConnection"/></returns>
        public static IEventStoreConnection Create(IPEndPoint tcpEndPoint, string connectionName = null)
        {
            return Create(ConnectionSettings.Default, tcpEndPoint, connectionName);
        }

        /// <summary>
        /// Creates a new <see cref="EventStoreConnection"/> using specific <see cref="ConnectionSettings"/>
        /// </summary>
        /// <param name="settings">The <see cref="ConnectionSettings"/> to apply to the new connection</param>
        /// <param name="tcpEndPoint">The <see cref="IPEndPoint"/> to connect to.</param>
        /// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
        /// <returns>a new <see cref="EventStoreConnection"/></returns>
        public static IEventStoreConnection Create(ConnectionSettings settings, IPEndPoint tcpEndPoint, string connectionName = null)
        {
            Ensure.NotNull(settings, "settings");
            Ensure.NotNull(tcpEndPoint, "tcpEndPoint");
            var tcpEndPointDiscoverer = Task.Factory.StartNew(() => tcpEndPoint);
            return new EventStoreNodeConnection(settings, () => tcpEndPointDiscoverer, connectionName);
        }
        /*
        /// <summary>
        /// Connects the <see cref="EventStoreConnection"/> synchronously to a clustered EventStore based upon DNS entry
        /// </summary>
        /// <param name="clusterDns">The cluster's DNS entry.</param>
        /// <param name="maxDiscoverAttempts">The maximum number of attempts to try the DNS.</param>
        /// <param name="port">The port to connect to.</param>
        public void Connect(string clusterDns, int maxDiscoverAttempts = Consts.DefaultMaxClusterDiscoverAttempts, int port = Consts.DefaultClusterManagerPort)
        {
            ConnectAsync(clusterDns, maxDiscoverAttempts, port).Wait();
        }

        /// <summary>
        /// Connects the <see cref="EventStoreConnection"/> asynchronously to a clustered EventStore based upon DNS entry
        /// </summary>
        /// <param name="clusterDns">The cluster's DNS entry</param>
        /// <param name="maxDiscoverAttempts">The maximum number of attempts to try the DNS</param>
        /// <param name="port">The port to connect to</param>
        /// <returns>A <see cref="Task"/> that can be awaited upon</returns>
        public Task ConnectAsync(string clusterDns,
                                 int maxDiscoverAttempts = Consts.DefaultMaxClusterDiscoverAttempts,
                                 int port = Consts.DefaultClusterManagerPort)
        {
            Ensure.NotNullOrEmpty(clusterDns, "clusterDns");
            Ensure.Positive(maxDiscoverAttempts, "maxDiscoverAttempts");
            Ensure.Nonnegative(port, "port");

            // TODO AN: rework this mess
            var explorer = new ClusterExplorer(_settings.Log, _settings.AllowForwarding, maxDiscoverAttempts, port);
            return explorer.Resolve(clusterDns)
                           .ContinueWith(resolve =>
                           {
                               if (resolve.IsFaulted || resolve.Result == null)
                                   throw new CannotEstablishConnectionException("Failed to find node to connect", resolve.Exception);
                               var endPoint = resolve.Result;
                               return EstablishConnectionAsync(endPoint);
                           });
        }
        */
    }
}