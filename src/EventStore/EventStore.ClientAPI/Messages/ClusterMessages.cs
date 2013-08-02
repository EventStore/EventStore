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

using System;

namespace EventStore.ClientAPI.Messages
{
    internal class ClusterMessages
    {
        public class ClusterInfoDto
        {
            public MemberInfoDto[] Members { get; set; }

            public ClusterInfoDto()
            {
            }

            public ClusterInfoDto(MemberInfoDto[] members)
            {
                Members = members;
            }
        }

        public class MemberInfoDto
        {
            public Guid InstanceId { get; set; }

            public DateTime TimeStamp { get; set; }
            public VNodeState State { get; set; }
            public bool IsAlive { get; set; }

            public string InternalTcpIp { get; set; }
            public int InternalTcpPort { get; set; }
            public int InternalSecureTcpPort { get; set; }
        
            public string ExternalTcpIp { get; set; }
            public int ExternalTcpPort { get; set; }
            public int ExternalSecureTcpPort { get; set; }

            public string InternalHttpIp { get; set; }
            public int InternalHttpPort { get; set; }

            public string ExternalHttpIp { get; set; }
            public int ExternalHttpPort { get; set; }
        
            public long LastCommitPosition { get; set; }
            public long WriterCheckpoint { get; set; }
            public long ChaserCheckpoint { get; set; }
        
            public long EpochPosition { get; set; }
            public int EpochNumber { get; set; }
            public Guid EpochId { get; set; }

            public override string ToString()
            {
                if (State == VNodeState.Manager)
                    return string.Format("MAN {0:B} <{1}> [{2}, {3}:{4}, {5}:{6}] | {7:yyyy-MM-dd HH:mm:ss.fff}",
                                         InstanceId, IsAlive ? "LIVE" : "DEAD", State,
                                         InternalHttpIp, InternalHttpPort,
                                         ExternalHttpIp, ExternalHttpPort,
                                         TimeStamp);
                return string.Format("VND {0:B} <{1}> [{2}, {3}:{4}, {5}, {6}:{7}, {8}, {9}:{10}, {11}:{12}] {13}/{14}/{15}/E{16}@{17}:{18:B} | {19:yyyy-MM-dd HH:mm:ss.fff}",
                                     InstanceId, IsAlive ? "LIVE" : "DEAD", State,
                                     InternalTcpIp, InternalTcpPort,
                                     InternalSecureTcpPort > 0 ? string.Format("{0}:{1}", InternalTcpIp, InternalSecureTcpPort) : "n/a",
                                     ExternalTcpIp, ExternalTcpPort,
                                     ExternalSecureTcpPort > 0 ? string.Format("{0}:{1}", ExternalTcpIp, ExternalSecureTcpPort) : "n/a",
                                     InternalHttpIp, InternalHttpPort, ExternalHttpIp, ExternalHttpPort,
                                     LastCommitPosition, WriterCheckpoint, ChaserCheckpoint,
                                     EpochNumber, EpochPosition, EpochId,
                                     TimeStamp);
            }
        }

        public enum VNodeState
        {
            Initializing,
            Unknown,
            PreReplica,
            CatchingUp,
            Clone,
            Slave,
            PreMaster,
            Master,
            Manager,
            ShuttingDown,
            Shutdown
        }
    }
}
