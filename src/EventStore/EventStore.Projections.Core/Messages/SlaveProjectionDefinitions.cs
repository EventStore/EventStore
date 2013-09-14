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
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Messages
{
    public sealed class SlaveProjectionDefinitions
    {

        public enum SlaveProjectionRequestedNumber
        {
            One,
            OnePerNode,
            OnePerThread
        }

        public sealed class Definition
        {
            private readonly string _name;
            private readonly string _handlerType;
            private readonly string _query;
            private readonly SlaveProjectionRequestedNumber _requestedNumber;
            private readonly ProjectionMode _mode;
            private readonly bool _emitEnabled;
            private readonly bool _checkpointsEnabled;
            private readonly bool _enableRunAs;
            private readonly ProjectionManagementMessage.RunAs _runAs;

            public Definition(
                string name, string handlerType, string query, SlaveProjectionRequestedNumber requestedNumber,
                ProjectionMode mode, bool emitEnabled, bool checkpointsEnabled, bool enableRunAs,
                ProjectionManagementMessage.RunAs runAs)
            {
                _name = name;
                _handlerType = handlerType;
                _query = query;
                _requestedNumber = requestedNumber;
                _runAs = runAs;
                _enableRunAs = enableRunAs;
                _checkpointsEnabled = checkpointsEnabled;
                _emitEnabled = emitEnabled;
                _mode = mode;
            }

            public string Name
            {
                get { return _name; }
            }

            public SlaveProjectionRequestedNumber RequestedNumber
            {
                get { return _requestedNumber; }
            }

            public ProjectionMode Mode
            {
                get { return _mode; }
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

            public ProjectionManagementMessage.RunAs RunAs
            {
                get { return _runAs; }
            }

            public string HandlerType
            {
                get { return _handlerType; }
            }

            public string Query
            {
                get { return _query; }
            }
        }

        private readonly Definition[] _definitions;

        public SlaveProjectionDefinitions(params Definition[] definitions)
        {
            _definitions = definitions;
        }

        public Definition[] Definitions
        {
            get { return _definitions; }
        }
    }
}
