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
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.VNode
{
    public class VNodeFSMUnoptimized: IHandle<Message>
    {
        private readonly Func<VNodeState> _getState;
        private readonly Dictionary<Type, Action<VNodeState, Message>>[] _handlers;
        private readonly Action<VNodeState, Message>[] _defaultHandlers;

        internal VNodeFSMUnoptimized(Func<VNodeState> getState, 
                                     Dictionary<Type, Action<VNodeState, Message>>[] handlers, 
                                     Action<VNodeState, Message>[] defaultHandlers)
        {
            _getState = getState;
            _handlers = handlers;
            _defaultHandlers = defaultHandlers;
        }

        public void Handle(Message message)
        {
            var state = _getState();
            var stateNum = (int)state;
            var handlers = _handlers[stateNum];

            var type = message.GetType();

            if (TryHandle(state, handlers, message, type))
                return;

            do
            {
                type = type.BaseType;
                if (TryHandle(state, handlers, message, type))
                    return;
            } while (type != typeof(Message));

            if (_defaultHandlers[stateNum] != null)
            {
                _defaultHandlers[stateNum](state, message);
                return;
            }

            throw new InvalidOperationException(
                string.Format("Unhandled message: {0} occured in state: {1}.", message, state));
        }

        private bool TryHandle(VNodeState state,
                               IDictionary<Type, Action<VNodeState, Message>> handlers,
                               Message message,
                               Type msgType)
        {
            if (handlers == null)
                return false;

            Action<VNodeState, Message> handler;
            if (!handlers.TryGetValue(msgType, out handler))
                return false;
            handler(state, message);
            return true;
        }
    }

    public class VNodeFSM : IHandle<Message>
    {
        private readonly Func<VNodeState> _getState;
        private readonly Action<VNodeState, Message>[][] _handlers;
        private readonly Action<VNodeState, Message>[] _defaultHandlers;

        internal VNodeFSM(Func<VNodeState> getState,
                          Dictionary<Type, Action<VNodeState, Message>>[] handlers,
                          Action<VNodeState, Message>[] defaultHandlers)
        {
            _getState = getState;

            _handlers = new Action<VNodeState, Message>[handlers.Length][];
            for (int i = 0; i < _handlers.Length; ++i)
            {
                _handlers[i] = new Action<VNodeState, Message>[MessageHierarchy.MaxMsgTypeId + 1];
                if (handlers[i] != null)
                {
                    foreach (var handler in handlers[i])
                    {
                        _handlers[i][MessageHierarchy.MsgTypeIdByType[handler.Key]] = handler.Value;
                    }
                }
            }

            _defaultHandlers = defaultHandlers;
        }

        public void Handle(Message message)
        {
            var state = _getState();
            var stateNum = (int)state;
            var handlers = _handlers[stateNum];

            var parents = MessageHierarchy.ParentsByTypeId[message.MsgTypeId];
            for (int i = 0; i < parents.Length; ++i)
            {
                if (TryHandle(state, handlers, message, parents[i]))
                    return;
            }

            if (_defaultHandlers[stateNum] != null)
            {
                _defaultHandlers[stateNum](state, message);
                return;
            }

            throw new Exception(string.Format("Unhandled message: {0} occurred in state: {1}.", message, state));
        }

        private static bool TryHandle(VNodeState state, Action<VNodeState, Message>[] handlers, Message message, int msgTypeId)
        {
            Action<VNodeState, Message> handler = handlers[msgTypeId];
            if (handler != null)
            {
                handler(state, message);
                return true;
            }
            return false;
        }
    }
}
