using System;
using EventStore.Projections.Core.Services.v8;
using System.Linq;

namespace EventStore.Projections.Core.Services.Management {
	public class ProjectionStateHandlerFactory {
		public IProjectionStateHandler Create(
			string factoryType, string source, Action<int, Action> cancelCallbackFactory = null,
			Action<string, object[]> logger = null) {
			var colonPos = factoryType.IndexOf(':');
			string kind = null;
			string rest = null;
			if (colonPos > 0) {
				kind = factoryType.Substring(0, colonPos);
				rest = factoryType.Substring(colonPos + 1);
			} else {
				kind = factoryType;
			}

			IProjectionStateHandler result;
			switch (kind.ToLowerInvariant()) {
				case "js":
					result = new DefaultV8ProjectionStateHandler(source, logger, cancelCallbackFactory);
					break;
				case "native":
					var type = Type.GetType(rest);
					if (type == null) {
						//TODO: explicitly list all the assemblies to look for handlers
						type =
							AppDomain.CurrentDomain.GetAssemblies()
								.Select(v => v.GetType(rest))
								.FirstOrDefault(v => v != null);
					}

					var handler = Activator.CreateInstance(type, source, logger);
					result = (IProjectionStateHandler)handler;
					break;
				default:
					throw new NotSupportedException(string.Format("'{0}' handler type is not supported", factoryType));
			}

			return result;
		}
	}
}
