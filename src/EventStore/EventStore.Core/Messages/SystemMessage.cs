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
using EventStore.Core.Cluster;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages
{
    public static class SystemMessage
    {
        public class SystemInit : Message
        {
        }

        public class SystemStart : Message
        {
        }

        public class StorageReaderInitializationDone: Message
        {
        }

        public class StorageWriterInitializationDone : Message
        {
        }

        public abstract class StateChangeMessage: Message
        {
            public readonly VNodeState State;

            protected StateChangeMessage(VNodeState state)
            {
                State = state;
            }
        }

        public class BecomeChaserCatchUp : StateChangeMessage
        {
            public readonly Guid CorrelationId;
            public readonly StateChangeMessage NextState;

            public BecomeChaserCatchUp(Guid correlationId, StateChangeMessage nextState): base(VNodeState.ChaserCatchUp)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(nextState, "nextState");

                CorrelationId = correlationId;
                NextState = nextState;
            }
        }

        public class BecomeMaster: StateChangeMessage
        {
            public BecomeMaster(): base(VNodeState.Master)
            {
            }
        }

        public class BecomeShuttingDown : StateChangeMessage
        {
            public BecomeShuttingDown(): base(VNodeState.ShuttingDown)
            {
            }
        }

        public class BecomeShutdown : StateChangeMessage
        {
            public BecomeShutdown(): base(VNodeState.Shutdown)
            {
            }
        }

        public class ServiceShutdown : Message
        {
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
        }

        public class VNodeConnectionLost : Message
        {
            public readonly IPEndPoint VNodeEndPoint;

            public VNodeConnectionLost(IPEndPoint vNodeEndPoint)
            {
                VNodeEndPoint = vNodeEndPoint;
            }
        }

        public class VNodeConnectionEstablished : Message
        {
            public readonly IPEndPoint VNodeEndPoint;

            public VNodeConnectionEstablished(IPEndPoint vNodeEndPoint)
            {
                VNodeEndPoint = vNodeEndPoint;
            }
        }

        public class ScavengeDatabase: Message
        {
        }

        public class WaitForChaserToCatchUp : Message
        {
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
            public readonly Guid CorrelationId;

            public ChaserCaughtUp(Guid correlationId)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");

                CorrelationId = correlationId;
            }
        }
    }
}