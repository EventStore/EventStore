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
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages
{
    public static class ProjectionManagementMessage
    {

        public class OperationFailed : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _reason;

            public OperationFailed(string reason)
            {
                _reason = reason;
            }

            public string Reason
            {
                get { return _reason; }
            }
        }

        public class NotFound : OperationFailed
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public NotFound()
                : base("Not Found")
            {
            }

        }

        public class NotAuthorized : OperationFailed
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public NotAuthorized()
                : base("Not authorized")
            {
            }
        }

        public class Conflict : OperationFailed
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public Conflict(string reason)
                : base(reason)
            {
            }

        }

        public sealed class RunAs
        {
            private readonly IPrincipal _runAs;

            public RunAs(IPrincipal runAs)
            {
                _runAs = runAs;
            }

            private static readonly RunAs _anonymous = new RunAs(null);
            private static readonly RunAs _system = new RunAs(SystemAccount.Principal);

            public static RunAs Anonymous
            {
                get { return _anonymous; }
            }

            public static RunAs System
            {
                get { return _system; }
            }

            public IPrincipal Principal
            {
                get { return _runAs; }
            }

            public static bool ValidateRunAs(ProjectionMode mode, ReadWrite readWrite, IPrincipal existingRunAs, ControlMessage message, bool replace = false)
            {
                if (mode > ProjectionMode.Transient && readWrite == ReadWrite.Write
                    && (message.RunAs == null || message.RunAs.Principal == null
                        || !message.RunAs.Principal.IsInRole(SystemRoles.Admins)))
                {
                    message.Envelope.ReplyWith(new NotAuthorized());
                    return false;
                }

                if (replace && message.RunAs.Principal == null)
                {
                    message.Envelope.ReplyWith(new NotAuthorized());
                    return false;
                }
                if (replace && message.RunAs.Principal != null)
                    return true; // enable this operation while no projection permissions are defined

                return true;

                //if (existingRunAs == null)
                //    return true;
                //if (message.RunAs.Principal == null
                //    || !string.Equals(
                //        existingRunAs.Identity.Name, message.RunAs.Principal.Identity.Name,
                //        StringComparison.OrdinalIgnoreCase))
                //{
                //    message.Envelope.ReplyWith(new NotAuthorized());
                //    return false;
                //}
                //return true;
            }

        }

        public abstract class ControlMessage: Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly IEnvelope _envelope;
            public readonly RunAs RunAs;

            protected ControlMessage(IEnvelope envelope, RunAs runAs)
            {
                _envelope = envelope;
                RunAs = runAs;
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }
        }

        public class Post : ControlMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly ProjectionMode _mode;
            private readonly string _name;
            private readonly string _handlerType;
            private readonly string _query;
            private readonly bool _enabled;
            private readonly bool _checkpointsEnabled;
            private readonly bool _emitEnabled;
            private readonly bool _enableRunAs;

            public Post(
                IEnvelope envelope, ProjectionMode mode, string name, RunAs runAs, string handlerType, string query,
                bool enabled, bool checkpointsEnabled, bool emitEnabled, bool enableRunAs = false)
                : base(envelope, runAs)
            {
                _name = name;
                _handlerType = handlerType;
                _mode = mode;
                _query = query;
                _enabled = enabled;
                _checkpointsEnabled = checkpointsEnabled;
                _emitEnabled = emitEnabled;
                _enableRunAs = enableRunAs;
            }

            public Post(
                IEnvelope envelope, ProjectionMode mode, string name, RunAs runAs, Type handlerType, string query,
                bool enabled, bool checkpointsEnabled, bool emitEnabled, bool enableRunAs = false)
                : base(envelope, runAs)
            {
                _name = name;
                _handlerType = "native:" + handlerType.Namespace + "." + handlerType.Name;
                _mode = mode;
                _query = query;
                _enabled = enabled;
                _checkpointsEnabled = checkpointsEnabled;
                _emitEnabled = emitEnabled;
                _enableRunAs = enableRunAs;
            }
            // shortcut for posting ad-hoc JS queries
            public Post(IEnvelope envelope, RunAs runAs, string query, bool enabled)
                : base(envelope, runAs)
            {
                _name = Guid.NewGuid().ToString("D");
                _handlerType = "JS";
                _mode = ProjectionMode.Transient;
                _query = query;
                _enabled = enabled;
                _checkpointsEnabled = false;
                _emitEnabled = false;
            }

            public ProjectionMode Mode
            {
                get { return _mode; }
            }

            public string Query
            {
                get { return _query; }
            }

            public string Name
            {
                get { return _name; }
            }

            public string HandlerType
            {
                get { return _handlerType; }
            }

            public bool Enabled
            {
                get { return _enabled; }
            }

            public bool EmitEnabled
            {
                get { return _emitEnabled; }
            }

            public bool CheckpointsEnabled
            {
                get { return _checkpointsEnabled; }
            }

            public bool EnableRunAs
            {
                get { return _enableRunAs; }
            }
        }

        public class Disable : ControlMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _name;

            public Disable(IEnvelope envelope, string name, RunAs runAs)
                : base(envelope, runAs)
            {
                _name = name;
            }

            public string Name
            {
                get { return _name; }
            }
        }

        public class Enable : ControlMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _name;

            public Enable(IEnvelope envelope, string name, RunAs runAs)
                : base(envelope, runAs)
            {
                _name = name;
            }

            public string Name
            {
                get { return _name; }
            }
        }

        public class Abort : ControlMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _name;

            public Abort(IEnvelope envelope, string name, RunAs runAs)
                : base(envelope, runAs)
            {
                _name = name;
            }

            public string Name
            {
                get { return _name; }
            }
        }

        public class SetRunAs : ControlMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public enum SetRemove
            {
                Set,
                Rmeove
            };

            private readonly string _name;
            private readonly SetRemove _action;

            public SetRunAs(IEnvelope envelope, string name, RunAs runAs, SetRemove action)
                : base(envelope, runAs)
            {
                _name = name;
                _action = action;
            }

            public string Name
            {
                get { return _name; }
            }

            public SetRemove Action
            {
                get { return _action; }
            }
        }

        public class UpdateQuery : ControlMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _name;
            private readonly string _handlerType;
            private readonly string _query;
            private readonly bool? _emitEnabled;

            public UpdateQuery(
                IEnvelope envelope, string name, RunAs runAs, string handlerType, string query, bool? emitEnabled)
                : base(envelope, runAs)
            {
                _name = name;
                _handlerType = handlerType;
                _query = query;
                _emitEnabled = emitEnabled;
            }

            public string Query
            {
                get { return _query; }
            }

            public string Name
            {
                get { return _name; }
            }

            public string HandlerType
            {
                get { return _handlerType; }
            }

            public bool? EmitEnabled
            {
                get { return _emitEnabled; }
            }
        }

        public class Reset : ControlMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _name;

            public Reset(IEnvelope envelope, string name, RunAs runAs)
                : base(envelope, runAs)
            {
                _name = name;
            }

            public string Name
            {
                get { return _name; }
            }
        }

        public class Delete : ControlMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _name;
            private readonly bool _deleteCheckpointStream;
            private readonly bool _deleteStateStream;

            public Delete(
                IEnvelope envelope, string name, RunAs runAs, bool deleteCheckpointStream, bool deleteStateStream)
                : base(envelope, runAs)
            {
                _name = name;
                _deleteCheckpointStream = deleteCheckpointStream;
                _deleteStateStream = deleteStateStream;
            }

            public string Name
            {
                get { return _name; }
            }

            public bool DeleteCheckpointStream
            {
                get { return _deleteCheckpointStream; }
            }

            public bool DeleteStateStream
            {
                get { return _deleteStateStream; }
            }
        }

        public class GetQuery : ControlMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _name;

            public GetQuery(IEnvelope envelope, string name, RunAs runAs):
                base(envelope, runAs)
            {
                _name = name;
            }

            public string Name
            {
                get { return _name; }
            }
        }

        public class Updated : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _name;

            public Updated(string name)
            {
                _name = name;
            }

            public string Name
            {
                get { return _name; }
            }
        }

        public class GetStatistics : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly IEnvelope _envelope;
            private readonly ProjectionMode? _mode;
            private readonly string _name;
            private readonly bool _includeDeleted;

            public GetStatistics(IEnvelope envelope, ProjectionMode? mode, string name, bool includeDeleted)
            {
                _envelope = envelope;
                _mode = mode;
                _name = name;
                _includeDeleted = includeDeleted;
            }

            public ProjectionMode? Mode
            {
                get { return _mode; }
            }

            public string Name
            {
                get { return _name; }
            }

            public bool IncludeDeleted
            {
                get { return _includeDeleted; }
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }
        }

        public class GetState : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly IEnvelope _envelope;
            private readonly string _name;
            private readonly string _partition;

            public GetState(IEnvelope envelope, string name, string partition)
            {
                if (envelope == null) throw new ArgumentNullException("envelope");
                if (name == null) throw new ArgumentNullException("name");
                if (partition == null) throw new ArgumentNullException("partition");
                _envelope = envelope;
                _name = name;
                _partition = partition;
            }

            public string Name
            {
                get { return _name; }
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

            public string Partition
            {
                get { return _partition; }
            }
        }

        public class GetResult : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly IEnvelope _envelope;
            private readonly string _name;
            private readonly string _partition;

            public GetResult(IEnvelope envelope, string name, string partition)
            {
                if (envelope == null) throw new ArgumentNullException("envelope");
                if (name == null) throw new ArgumentNullException("name");
                if (partition == null) throw new ArgumentNullException("partition");
                _envelope = envelope;
                _name = name;
                _partition = partition;
            }

            public string Name
            {
                get { return _name; }
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

            public string Partition
            {
                get { return _partition; }
            }
        }

        public class Statistics : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly ProjectionStatistics[] _projections;

            public Statistics(ProjectionStatistics[] projections)
            {
                _projections = projections;
            }

            public ProjectionStatistics[] Projections
            {
                get { return _projections; }
            }

        }

        public abstract class ProjectionDataBase : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _name;
            private readonly string _partition;
            private readonly CheckpointTag _position;
            private readonly Exception _exception;

            protected ProjectionDataBase(
                string name, string partition, CheckpointTag position, Exception exception = null)
            {
                _name = name;
                _partition = partition;
                _position = position;
                _exception = exception;
            }

            public string Name
            {
                get { return _name; }
            }

            public Exception Exception
            {
                get { return _exception; }
            }

            public string Partition
            {
                get { return _partition; }
            }

            public CheckpointTag Position
            {
                get { return _position; }
            }
        }

        public class ProjectionState : ProjectionDataBase
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _state;

            public ProjectionState(
                string name, string partition, string state, CheckpointTag position, Exception exception = null)
                : base(name, partition, position, exception)
            {
                _state = state;
            }

            public string State
            {
                get { return _state; }
            }
        }

        public class ProjectionResult : ProjectionDataBase
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _result;

            public ProjectionResult(
                string name, string partition, string result, CheckpointTag position, Exception exception = null)
                : base(name, partition, position, exception)
            {
                _result = result;
            }

            public string Result
            {
                get { return _result; }
            }
        }

        public class ProjectionQuery : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _name;
            private readonly string _query;
            private readonly bool _emitEnabled;
            private readonly ProjectionSourceDefinition _definition;

            public ProjectionQuery(string name, string query, bool emitEnabled, ProjectionSourceDefinition definition)
            {
                _name = name;
                _query = query;
                _emitEnabled = emitEnabled;
                _definition = definition;
            }

            public string Name
            {
                get { return _name; }
            }

            public string Query
            {
                get { return _query; }
            }

            public bool EmitEnabled
            {
                get { return _emitEnabled; }
            }

            public ProjectionSourceDefinition Definition
            {
                get { return _definition; }
            }
        }

        public static class Internal
        {
            public class CleanupExpired: Message
            {
                private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
                public override int MsgTypeId { get { return TypeId; } }
            }

            public class RegularTimeout : Message
            {
                private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
                public override int MsgTypeId { get { return TypeId; } }
            }

            public class Deleted : Message
            {
                private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
                public override int MsgTypeId { get { return TypeId; } }

                private readonly string _name;
                private readonly Guid _id;

                public Deleted(string name, Guid id)
                {
                    _name = name;
                    _id = id;
                }

                public string Name
                {
                    get { return _name; }
                }

                public Guid Id
                {
                    get { return _id; }
                }
            }
        }

        public sealed class RequestSystemProjections : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly IEnvelope Envelope;

            public RequestSystemProjections(IEnvelope envelope)
            {
                Envelope = envelope;
            }
        }

        public sealed class RegisterSystemProjection : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string Name;
            public readonly string Handler;
            public readonly string Query;

            public RegisterSystemProjection(string name, string handler, string query)
            {
                Name = name;
                Handler = handler;
                Query = query;
            }
        }

        public class StartSlaveProjections : ControlMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _name;
            private readonly SlaveProjectionDefinitions _slaveProjections;
            private readonly IPublisher _resultsPublisher;
            private readonly Guid _masterCorrelationId;

            public StartSlaveProjections(
                IEnvelope envelope, RunAs runAs, string name, SlaveProjectionDefinitions slaveProjections,
                IPublisher resultsPublisher, Guid masterCorrelationId)
                : base(envelope, runAs)
            {
                _name = name;
                _slaveProjections = slaveProjections;
                _resultsPublisher = resultsPublisher;
                _masterCorrelationId = masterCorrelationId;
            }

            public string Name
            {
                get { return _name; }
            }

            public SlaveProjectionDefinitions SlaveProjections
            {
                get { return _slaveProjections; }
            }

            public IPublisher ResultsPublisher
            {
                get { return _resultsPublisher; }
            }

            public Guid MasterCorrelationId
            {
                get { return _masterCorrelationId; }
            }
        }

        public class SlaveProjectionsStarted : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly Guid _coreProjectionCorrelationId;
            private readonly SlaveProjectionCommunicationChannels _slaveProjections;

            public SlaveProjectionsStarted(Guid coreProjectionCorrelationId, SlaveProjectionCommunicationChannels slaveProjections)
            {
                _coreProjectionCorrelationId = coreProjectionCorrelationId;
                _slaveProjections = slaveProjections;
            }

            public Guid CoreProjectionCorrelationId
            {
                get { return _coreProjectionCorrelationId; }
            }

            public SlaveProjectionCommunicationChannels SlaveProjections
            {
                get { return _slaveProjections; }
            }
        }
    }
}