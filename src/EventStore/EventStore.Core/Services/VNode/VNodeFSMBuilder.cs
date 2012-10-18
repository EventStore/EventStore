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
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Cluster;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.VNode
{
    /// <summary>
    /// Builder syntax for constructing <see cref="VNodeFSM"/> in the code
    /// </summary>
    public class VNodeFSMBuilder
    {
        private readonly Func<VNodeState> _getState;
        private readonly Dictionary<Type, Action<VNodeState, Message>>[] _handlers;
        private readonly Action<VNodeState, Message>[] _defaultHandlers;

        public VNodeFSMBuilder(Func<VNodeState> getState)
        {
            _getState = getState;

            var maxState = Enum.GetValues(typeof (VNodeState)).Cast<int>().Max();
            _handlers = new Dictionary<Type, Action<VNodeState, Message>>[maxState + 1];
            _defaultHandlers = new Action<VNodeState, Message>[maxState + 1];
        }

        internal void AddHandler<TActualMessage>(VNodeState state, Action<VNodeState, Message> handler)
            where TActualMessage: Message
        {
            var stateNum = (int) state;
            
            Dictionary<Type, Action<VNodeState, Message>> stateHandlers = _handlers[stateNum];
            if (stateHandlers == null)
                stateHandlers = _handlers[stateNum] = new Dictionary<Type, Action<VNodeState, Message>>();

            //var existingHandler = stateHandlers[typeof (TActualMessage)];
            //stateHandlers[typeof (TActualMessage)] = existingHandler == null
            //                                            ? handler
            //                                            : (s, m) => { existingHandler(s, m); handler(s, m); };

            if (stateHandlers.ContainsKey(typeof(TActualMessage)))
                throw new InvalidOperationException(
                    string.Format("Handler already defined for state {0} and message {1}",
                                  state,
                                  typeof (TActualMessage).FullName));
            stateHandlers[typeof(TActualMessage)] = handler;
        }

        internal void AddDefaultHandler(VNodeState state, Action<VNodeState, Message> handler)
        {
            var stateNum = (int)state;
            //var existingHandler = _defaultHandlers[stateNum];
            //_defaultHandlers[stateNum] = existingHandler == null
            //                                ? handler
            //                                : (s, m) => { existingHandler(s, m); handler(s, m); };
            if (_defaultHandlers[stateNum] != null)
                throw new InvalidOperationException(string.Format("Default handler already defined for state {0}", state));
            _defaultHandlers[stateNum] = handler;
        }

        public VNodeFSMStatesDefinition InAnyState()
        {
            var allStates = Enum.GetValues(typeof (VNodeState)).Cast<VNodeState>().ToArray();
            return new VNodeFSMStatesDefinition(this, allStates);
        }

        public VNodeFSMStatesDefinition InState(VNodeState state)
        {
            return new VNodeFSMStatesDefinition(this, state);
        }

        public VNodeFSMStatesDefinition InStates(params VNodeState[] states)
        {
            return new VNodeFSMStatesDefinition(this, states);
        }

        public VNodeFSMStatesDefinition InOtherStates()
        {
            var unspecifiedStates = _handlers.Select((x, i) => x == null ? i : -1)
                .Where(x => x >= 0)
                .Cast<VNodeState>()
                .ToArray();
            return new VNodeFSMStatesDefinition(this, unspecifiedStates);
        }

        public VNodeFSM Build()
        {
            return new VNodeFSM(_getState, _handlers, _defaultHandlers);
        }
    }
}