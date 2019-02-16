using System;
using System.Collections.Generic;
using System.Security.Principal;
using EventStore.Common.Log;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.Transport.Tcp {
	public abstract class TcpDispatcher : ITcpDispatcher {
		private static readonly ILogger Log = LogManager.GetLoggerFor<TcpDispatcher>();

		private readonly Func<TcpPackage, IEnvelope, IPrincipal, string, string, TcpConnectionManager, Message>[][]
			_unwrappers;

		private readonly IDictionary<Type, Func<Message, TcpPackage>>[] _wrappers;

		protected TcpDispatcher() {
			_unwrappers =
				new Func<TcpPackage, IEnvelope, IPrincipal, string, string, TcpConnectionManager, Message>[2][];
			_unwrappers[0] =
				new Func<TcpPackage, IEnvelope, IPrincipal, string, string, TcpConnectionManager, Message>[255];
			_unwrappers[1] =
				new Func<TcpPackage, IEnvelope, IPrincipal, string, string, TcpConnectionManager, Message>[255];

			_wrappers = new Dictionary<Type, Func<Message, TcpPackage>>[2];
			_wrappers[0] = new Dictionary<Type, Func<Message, TcpPackage>>();
			_wrappers[1] = new Dictionary<Type, Func<Message, TcpPackage>>();
		}

		protected void AddWrapper<T>(Func<T, TcpPackage> wrapper, ClientVersion version) where T : Message {
			_wrappers[(byte)version][typeof(T)] = x => wrapper((T)x);
		}

		protected void AddUnwrapper<T>(TcpCommand command, Func<TcpPackage, IEnvelope, T> unwrapper,
			ClientVersion version) where T : Message {
			_unwrappers[(byte)version][(byte)command] = (pkg, env, user, login, pass, conn) => unwrapper(pkg, env);
		}

		protected void AddUnwrapper<T>(TcpCommand command,
			Func<TcpPackage, IEnvelope, TcpConnectionManager, T> unwrapper, ClientVersion version) where T : Message {
			_unwrappers[(byte)version][(byte)command] =
				(pkg, env, user, login, pass, conn) => unwrapper(pkg, env, conn);
		}

		protected void AddUnwrapper<T>(TcpCommand command, Func<TcpPackage, IEnvelope, IPrincipal, T> unwrapper,
			ClientVersion version) where T : Message {
			_unwrappers[(byte)version][(byte)command] =
				(pkg, env, user, login, pass, conn) => unwrapper(pkg, env, user);
		}

		protected void AddUnwrapper<T>(TcpCommand command,
			Func<TcpPackage, IEnvelope, IPrincipal, string, string, T> unwrapper, ClientVersion version)
			where T : Message {
			_unwrappers[(byte)version][(byte)command] = (pkg, env, user, login, pass, conn) =>
				unwrapper(pkg, env, user, login, pass);
		}

		protected void AddUnwrapper<T>(TcpCommand command,
			Func<TcpPackage, IEnvelope, IPrincipal, string, string, TcpConnectionManager, T> unwrapper,
			ClientVersion version)
			where T : Message {
// ReSharper disable RedundantCast
			_unwrappers[(byte)version][(byte)command] =
				(Func<TcpPackage, IEnvelope, IPrincipal, string, string, TcpConnectionManager, Message>)unwrapper;
// ReSharper restore RedundantCast
		}

		public TcpPackage? WrapMessage(Message message, byte version) {
			if (message == null)
				throw new ArgumentNullException("message");

			try {
				Func<Message, TcpPackage> wrapper;
				if (_wrappers[version].TryGetValue(message.GetType(), out wrapper))
					return wrapper(message);
				if (_wrappers[_wrappers.Length - 1].TryGetValue(message.GetType(), out wrapper))
					return wrapper(message);
			} catch (Exception exc) {
				Log.ErrorException(exc, "Error while wrapping message {message}.", message);
			}

			return null;
		}

		public Message UnwrapPackage(TcpPackage package, IEnvelope envelope, IPrincipal user, string login, string pass,
			TcpConnectionManager connection, byte version) {
			if (envelope == null)
				throw new ArgumentNullException("envelope");

			var unwrapper = _unwrappers[version][(byte)package.Command];
			if (unwrapper == null) {
				unwrapper = _unwrappers[_unwrappers.Length - 1][(byte)package.Command];
			}

			if (unwrapper != null) {
				try {
					return unwrapper(package, envelope, user, login, pass, connection);
				} catch (Exception exc) {
					Log.ErrorException(exc, "Error while unwrapping TcpPackage with command {command}.",
						package.Command);
				}
			}

			return null;
		}
	}
}
