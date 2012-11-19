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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.Tests.ClientAPI.Helpers
{
    internal class MiniNode
    {
        private static readonly ConcurrentQueue<int> AvailablePorts = new ConcurrentQueue<int>(GetRandomPorts(10000, 10000));

        public IPEndPoint TcpEndPoint { get; private set; }
        public IPEndPoint HttpEndPoint { get; private set; }

        private readonly SingleVNode _node;
        private readonly TFChunkDb _tfChunkDb;
        private ICheckpoint _writerChk;
        private ICheckpoint _chaserChk;
        private readonly string _dbPath;

        public MiniNode()
        {
            int extTcpPort;
            int extHttpPort;
            if (!AvailablePorts.TryDequeue(out extTcpPort))
                throw new Exception("Couldn't get free external TCP port for MiniNode.");
            if (!AvailablePorts.TryDequeue(out extHttpPort))
                throw new Exception("Couldn't get free external HTTP port for MiniNode.");

            _dbPath = Path.Combine(Path.GetTempPath(),
                                   Guid.NewGuid().ToString(),
                                   string.Format("mini-node-db-{0}-{1}", extTcpPort, extHttpPort));
            Directory.CreateDirectory(_dbPath);
            _tfChunkDb = new TFChunkDb(CreateOneTimeDbConfig(1*1024*1024, _dbPath, 2));

            var ip = GetLocalIp();
            TcpEndPoint = new IPEndPoint(ip, extTcpPort);
            HttpEndPoint = new IPEndPoint(ip, extHttpPort);

            var singleVNodeSettings = new SingleVNodeSettings(TcpEndPoint, HttpEndPoint, new[] {HttpEndPoint.ToHttpUrl()});
            var appSettings = new SingleVNodeAppSettings(TimeSpan.FromHours(1), StatsStorage.None);

            _node = new SingleVNode(_tfChunkDb, singleVNodeSettings, appSettings, dbVerifyHashes: true);
        }

        private static int[] GetRandomPorts(int from, int portCount)
        {
            var res = new int[portCount];
            var rnd = new Random(Guid.NewGuid().GetHashCode());
            for (int i = 0; i < portCount; ++i)
            {
                res[i] = from + i;
            }
            for (int i = 0; i < portCount; ++i)
            {
                int index = rnd.Next(portCount - i);
                int tmp = res[i];
                res[i] = res[i + index];
                res[i + index] = tmp;
            }
            return res;
        }

        public void Start()
        {
            var startedEvent = new ManualResetEventSlim(false);
            _node.Bus.Subscribe(new AdHocHandler<SystemMessage.SystemStart>(m => startedEvent.Set()));

            _node.Start();

            if (!startedEvent.Wait(20000))
                throw new TimeoutException("MiniNode haven't started in 20 seconds.");

            Thread.Sleep(100);
        }

        public void Shutdown()
        {
            var shutdownEvent = new ManualResetEventSlim(false);
            _node.Bus.Subscribe(new AdHocHandler<SystemMessage.BecomeShutdown>(m => shutdownEvent.Set()));

            _node.Stop();

            if (!shutdownEvent.Wait(20000))
                throw new TimeoutException("MiniNode haven't shut down in 20 seconds.");

            Thread.Sleep(100);

            AvailablePorts.Enqueue(TcpEndPoint.Port);
            AvailablePorts.Enqueue(HttpEndPoint.Port);

            _chaserChk.Dispose();
            _writerChk.Dispose();
            _tfChunkDb.Dispose();

            TryDeleteDirectory(_dbPath);
        }

        private void TryDeleteDirectory(string directory)
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to remove directory {0}", directory);
                Debug.WriteLine(e);
            }
        }

        private IPAddress GetLocalIp()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        }

        private TFChunkDbConfig CreateOneTimeDbConfig(int chunkSize, string dbPath, int chunksToCache)
        {
            if (Runtime.IsMono)
            {
                _writerChk = new FileCheckpoint(Path.Combine(dbPath, Checkpoint.Writer + ".chk"), Checkpoint.Writer, cached: true);
                _chaserChk = new FileCheckpoint(Path.Combine(dbPath, Checkpoint.Chaser + ".chk"), Checkpoint.Chaser, cached: true);
            }
            else
            {
                _writerChk = new MemoryMappedFileCheckpoint(Path.Combine(dbPath, Checkpoint.Writer + ".chk"), Checkpoint.Writer, cached: true);
                _chaserChk = new MemoryMappedFileCheckpoint(Path.Combine(dbPath, Checkpoint.Chaser + ".chk"), Checkpoint.Chaser, cached: true);
            }
            var nodeConfig = new TFChunkDbConfig(dbPath,
                                                 new VersionedPatternFileNamingStrategy(dbPath, "chunk-"),
                                                 chunkSize,
                                                 chunksToCache,
                                                 _writerChk,
                                                 _chaserChk,
                                                 new[] {_writerChk, _chaserChk});

            return nodeConfig;
        }
    }
}
