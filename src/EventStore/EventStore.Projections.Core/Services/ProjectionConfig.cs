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

namespace EventStore.Projections.Core.Services
{
    public class ProjectionConfig
    {
        private readonly IPrincipal _runAs;
        private readonly int _checkpointHandledThreshold;
        private readonly int _checkpointUnhandledBytesThreshold;
        private readonly int _pendingEventsThreshold;
        private readonly int _maxWriteBatchLength;
        private readonly bool _emitEventEnabled;
        private readonly bool _checkpointsEnabled;
        private readonly bool _createTempStreams;
        private readonly bool _stopOnEof;
        private readonly bool _isSlaveProjection;

        public ProjectionConfig(IPrincipal runAs, int checkpointHandledThreshold, int checkpointUnhandledBytesThreshold,
            int pendingEventsThreshold, int maxWriteBatchLength, bool emitEventEnabled, bool checkpointsEnabled,
            bool createTempStreams, bool stopOnEof, bool isSlaveProjection)
        {
            if (checkpointsEnabled)
            {
                if (checkpointHandledThreshold <= 0)
                    throw new ArgumentOutOfRangeException("checkpointHandledThreshold");
                if (checkpointUnhandledBytesThreshold < checkpointHandledThreshold)
                    throw new ArgumentException("Checkpoint threshold cannot be less than checkpoint handled threshold");
            }
            else
            {
                if (checkpointHandledThreshold != 0)
                    throw new ArgumentOutOfRangeException("checkpointHandledThreshold must be 0");
                if (checkpointUnhandledBytesThreshold != 0)
                    throw new ArgumentException("checkpointUnhandledBytesThreshold must be 0");
            }
            _runAs = runAs;
            _checkpointHandledThreshold = checkpointHandledThreshold;
            _checkpointUnhandledBytesThreshold = checkpointUnhandledBytesThreshold;
            _pendingEventsThreshold = pendingEventsThreshold;
            _maxWriteBatchLength = maxWriteBatchLength;
            _emitEventEnabled = emitEventEnabled;
            _checkpointsEnabled = checkpointsEnabled;
            _createTempStreams = createTempStreams;
            _stopOnEof = stopOnEof;
            _isSlaveProjection = isSlaveProjection;
        }

        public int CheckpointHandledThreshold
        {
            get { return _checkpointHandledThreshold; }
        }

        public int CheckpointUnhandledBytesThreshold
        {
            get { return _checkpointUnhandledBytesThreshold; }
        }

        public int MaxWriteBatchLength
        {
            get { return _maxWriteBatchLength; }
        }

        public bool EmitEventEnabled
        {
            get { return _emitEventEnabled; }
        }

        public bool CheckpointsEnabled
        {
            get { return _checkpointsEnabled; }
        }

        public int PendingEventsThreshold
        {
            get { return _pendingEventsThreshold; }
        }

        public bool CreateTempStreams
        {
            get { return _createTempStreams; }
        }

        public bool StopOnEof
        {
            get { return _stopOnEof; }
        }

        public IPrincipal RunAs
        {
            get { return _runAs; }
        }

        public bool IsSlaveProjection
        {
            get { return _isSlaveProjection; }
        }

        public static ProjectionConfig GetTest()
        {
            return new ProjectionConfig(null, 1000, 1000*1000, 100, 500, true, true, false, false, false);
        }
    }
}