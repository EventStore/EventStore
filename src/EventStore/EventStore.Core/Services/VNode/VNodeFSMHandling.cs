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
using EventStore.Core.Cluster;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.VNode
{
    public class VNodeFSMHandling<TMessage> where TMessage: Message
    {
        private readonly VNodeFSMStatesDefinition _stateDef;
        private readonly bool _defaultHandler;

        public VNodeFSMHandling(VNodeFSMStatesDefinition stateDef, bool defaultHandler = false)
        {
            _stateDef = stateDef;
            _defaultHandler = defaultHandler;
        }

        public VNodeFSMStatesDefinition Do(Action<VNodeState, TMessage> handler)
        {
            foreach (var state in _stateDef.States)
            {
                if (_defaultHandler)
                    _stateDef.FSM.AddDefaultHandler(state, (s, m) => handler(s, (TMessage)m));
                else
                    _stateDef.FSM.AddHandler<TMessage>(state, (s, m) => handler(s, (TMessage)m));
            }
            return _stateDef;
        }

        public VNodeFSMStatesDefinition Do(Action<TMessage> handler)
        {
            foreach (var state in _stateDef.States)
            {
                if (_defaultHandler)
                    _stateDef.FSM.AddDefaultHandler(state, (s, m) => handler((TMessage)m));
                else
                    _stateDef.FSM.AddHandler<TMessage>(state, (s, m) => handler((TMessage)m));
            }
            return _stateDef;
        }

        public VNodeFSMStatesDefinition Ignore()
        {
            foreach (var state in _stateDef.States)
            {
                if (_defaultHandler)
                    _stateDef.FSM.AddDefaultHandler(state, (s, m) => { });
                else
                    _stateDef.FSM.AddHandler<TMessage>(state, (s, m) => { });    
            }
            return _stateDef;
        }

        public VNodeFSMStatesDefinition Throw()
        {
            foreach (var state in _stateDef.States)
            {
                if (_defaultHandler)
                    _stateDef.FSM.AddDefaultHandler(state, (s, m) => { throw new NotSupportedException(); });
                else
                    _stateDef.FSM.AddHandler<TMessage>(state, (s, m) => { throw new NotSupportedException(); });
            }
            return _stateDef;
        }
    }
}