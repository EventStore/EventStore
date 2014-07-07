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