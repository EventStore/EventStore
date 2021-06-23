using System;
using EventStore.Projections.Core.Services.v8;
using System.Linq;
using EventStore.Common;
using EventStore.Common.Options;
using EventStore.Projections.Core.Services.Interpreted;
using ProtoBuf.Meta;

namespace EventStore.Projections.Core.Services.Management {

	
	public class ProjectionStateHandlerFactory {
		private readonly Func<string, Action<string, object[]>, Action<int, Action>, bool, IProjectionStateHandler> _jsFactory;
		public ProjectionStateHandlerFactory(TimeSpan javascriptCompilationTimeout, TimeSpan javascriptExecutionTimeout, JavascriptProjectionRuntime runtime) {
			
			_jsFactory = runtime switch {
				JavascriptProjectionRuntime.Legacy => (source, logger, cancelCallbackFactory, enableContentTypeValidation) =>
					new DefaultV8ProjectionStateHandler(source, logger, cancelCallbackFactory, enableContentTypeValidation),
				JavascriptProjectionRuntime.Interpreted => (source, _, _, enableContentTypeValidation) => 
					new JintProjectionStateHandler(source, enableContentTypeValidation, javascriptCompilationTimeout, javascriptExecutionTimeout),
				_ => throw new ArgumentOutOfRangeException(nameof(runtime), runtime, "Unknown javascript projection runtime")
			};
		}
		public IProjectionStateHandler Create(
			string factoryType, string source,
			bool enableContentTypeValidation,
			Action<int, Action> cancelCallbackFactory = null,
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
					result = _jsFactory(source, logger, cancelCallbackFactory, enableContentTypeValidation);
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
