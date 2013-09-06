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
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages
{
    public static class SystemMessage
    {
        public class SystemInit : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }
        }

        public class SystemStart : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }
        }

        public class ServiceInitialized: Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string ServiceName;

            public ServiceInitialized(string serviceName)
            {
                Ensure.NotNullOrEmpty(serviceName, "serviceName");
                ServiceName = serviceName;
            }
        }

        public class WriteEpoch: Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }
        }

        public abstract class StateChangeMessage: Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;
            public readonly VNodeState State;

            protected StateChangeMessage(Guid correlationId, VNodeState state)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                CorrelationId = correlationId;
                State = state;
            }
        }

        public class BecomePreMaster : StateChangeMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public BecomePreMaster(Guid correlationId): base(correlationId, VNodeState.PreMaster)
            {
            }
        }

        public class BecomeMaster: StateChangeMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public BecomeMaster(Guid correlationId): base(correlationId, VNodeState.Master)
            {
            }
        }

        public class BecomeShuttingDown : StateChangeMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly bool ExitProcess;

            public BecomeShuttingDown(Guid correlationId, bool exitProcess): base(correlationId, VNodeState.ShuttingDown)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                ExitProcess = exitProcess;
            }
        }

        public class BecomeShutdown : StateChangeMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public BecomeShutdown(Guid correlationId): base(correlationId, VNodeState.Shutdown)
            {
            }
        }

        public class BecomeUnknown : StateChangeMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public BecomeUnknown(Guid correlationId)
                : base(correlationId, VNodeState.Unknown)
            {
            }
        }

        public abstract class ReplicaStateMessage : StateChangeMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly VNodeInfo Master;

            protected ReplicaStateMessage(Guid correlationId, VNodeState state, VNodeInfo master)
                : base(correlationId, state)
            {
                Ensure.NotNull(master, "master");
                Master = master;
            }
        }

        public class BecomePreReplica : ReplicaStateMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public BecomePreReplica(Guid correlationId, VNodeInfo master): base(correlationId, VNodeState.PreReplica, master)
            {
            }
        }

        public class BecomeCatchingUp : ReplicaStateMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public BecomeCatchingUp(Guid correlationId, VNodeInfo master): base(correlationId, VNodeState.CatchingUp, master)
            {
            }
        }

        public class BecomeClone : ReplicaStateMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public BecomeClone(Guid correlationId, VNodeInfo master): base(correlationId, VNodeState.Clone, master)
            {
            }
        }

        public class BecomeSlave : ReplicaStateMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public BecomeSlave(Guid correlationId, VNodeInfo master): base(correlationId, VNodeState.Slave, master)
            {
            }
        }

        public class ServiceShutdown : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string ServiceName;

            public ServiceShutdown(string serviceName)
            {
                if (string.IsNullOrEmpty(serviceName)) 
                    throw new ArgumentNullException("serviceName");
                ServiceName = serviceName;
            }
        }

        public class ShutdownTimeout : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }
        }

        public class VNodeConnectionLost : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly IPEndPoint VNodeEndPoint;
            public readonly Guid ConnectionId;

            public VNodeConnectionLost(IPEndPoint vNodeEndPoint, Guid connectionId)
            {
                Ensure.NotNull(vNodeEndPoint, "vNodeEndPoint");
                Ensure.NotEmptyGuid(connectionId, "connectionId");

                VNodeEndPoint = vNodeEndPoint;
                ConnectionId = connectionId;
            }
        }

        public class VNodeConnectionEstablished : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly IPEndPoint VNodeEndPoint;
            public readonly Guid ConnectionId;

            public VNodeConnectionEstablished(IPEndPoint vNodeEndPoint, Guid connectionId)
            {
                Ensure.NotNull(vNodeEndPoint, "vNodeEndPoint");
                Ensure.NotEmptyGuid(connectionId, "connectionId");

                VNodeEndPoint = vNodeEndPoint;
                ConnectionId = connectionId;
            }
        }

        public class WaitForChaserToCatchUp : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;
            public readonly TimeSpan TotalTimeWasted;

            public WaitForChaserToCatchUp(Guid correlationId, TimeSpan totalTimeWasted)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");

                CorrelationId = correlationId;
                TotalTimeWasted = totalTimeWasted;
            }
        }

        public class ChaserCaughtUp : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;

            public ChaserCaughtUp(Guid correlationId)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                CorrelationId = correlationId;
            }
        }
    }
}