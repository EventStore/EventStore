using System;
using System.IO;
using System.Reflection;
using EventStore.Common.Utils;

namespace EventStore.Projections.Core.Services.v8 {
	public class DefaultV8ProjectionStateHandler : V8ProjectionStateHandler {
		public DefaultV8ProjectionStateHandler(
			string query, Action<string, object[]> logger, Action<int, Action> cancelCallbackFactory, bool enableContentTypeValidation)
			: base("1Prelude", query, GetModuleSource, logger, cancelCallbackFactory, enableContentTypeValidation) {
		}

		public static Tuple<string, string> GetModuleSource(string name) {
			var resourceName = string.Format("{0}.{1}.js", Locations.PreludeResourcesPath, name);
			var assembly = Assembly.GetAssembly(typeof(ProjectionManagerNode));
			using (Stream stream = assembly.GetManifestResourceStream(resourceName))
			using (StreamReader reader = new StreamReader(stream, Helper.UTF8NoBom)) {
				var result = reader.ReadToEnd();
				return Tuple.Create(result, resourceName);
			}
		}
	}
}
