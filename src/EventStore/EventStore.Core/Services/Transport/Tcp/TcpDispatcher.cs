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
using System.Security.Principal;
using EventStore.Common.Log;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.Transport.Tcp
{
    public abstract class TcpDispatcher: ITcpDispatcher
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<TcpDispatcher>();

        private readonly Func<TcpPackage, IEnvelope, IPrincipal, string, string, TcpConnectionManager, Message>[] _unwrappers;
        private readonly IDictionary<Type, Func<Message, TcpPackage>> _wrappers;

        protected TcpDispatcher()
        {
            _unwrappers = new Func<TcpPackage, IEnvelope, IPrincipal, string, string, TcpConnectionManager, Message>[255];
            _wrappers = new Dictionary<Type, Func<Message, TcpPackage>>();
        }

        protected void AddWrapper<T>(Func<T, TcpPackage> wrapper) where T : Message
        {
            _wrappers[typeof(T)] = x => wrapper((T)x);
        }

        protected void AddUnwrapper<T>(TcpCommand command, Func<TcpPackage, IEnvelope, T> unwrapper) where T : Message
        {
            _unwrappers[(byte)command] = (pkg, env, user, login, pass, conn) => unwrapper(pkg, env);
        }

        protected void AddUnwrapper<T>(TcpCommand command, Func<TcpPackage, IEnvelope, TcpConnectionManager, T> unwrapper) where T : Message
        {
            _unwrappers[(byte)command] = (pkg, env, user, login, pass, conn) => unwrapper(pkg, env, conn);
        }

        protected void AddUnwrapper<T>(TcpCommand command, Func<TcpPackage, IEnvelope, IPrincipal, T> unwrapper) where T : Message
        {
            _unwrappers[(byte)command] = (pkg, env, user, login, pass, conn) => unwrapper(pkg, env, user);
        }

        protected void AddUnwrapper<T>(TcpCommand command, Func<TcpPackage, IEnvelope, IPrincipal, string, string, T> unwrapper) where T : Message
        {
            _unwrappers[(byte)command] = (pkg, env, user, login, pass, conn) => unwrapper(pkg, env, user, login, pass);
        }

        protected void AddUnwrapper<T>(TcpCommand command, Func<TcpPackage, IEnvelope, IPrincipal, string, string, TcpConnectionManager, T> unwrapper) 
            where T : Message
        {
// ReSharper disable RedundantCast
            _unwrappers[(byte) command] = (Func<TcpPackage, IEnvelope, IPrincipal, string, string, TcpConnectionManager, Message>) unwrapper;
// ReSharper restore RedundantCast
        }

        public TcpPackage? WrapMessage(Message message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            try
            {
                Func<Message, TcpPackage> wrapper;
                if (_wrappers.TryGetValue(message.GetType(), out wrapper))
                    return wrapper(message);
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Error while wrapping message {0}.", message);
            }
            return null;
        }

        public Message UnwrapPackage(TcpPackage package, IEnvelope envelope, IPrincipal user, string login, string pass, TcpConnectionManager connection)
        {
            if (envelope == null)
                throw new ArgumentNullException("envelope");

            var unwrapper = _unwrappers[(byte)package.Command];
            if (unwrapper != null)
            {
                try
                {
                    return unwrapper(package, envelope, user, login, pass, connection);
                }
                catch (Exception exc)
                {
                    Log.ErrorException(exc, "Error while unwrapping TcpPackage with command {0}.", package.Command);
                }
            }
            return null;
        }
    }
}