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
