using System.Linq;
using System.Net.Http.Headers;

namespace EventStore.Core.Tests.Http {
	internal static class HttpResponseHeaderExtensions {
		public static string GetLocationAsString(this HttpResponseHeaders headers)
			=> headers.TryGetValues("location", out var values)
				? values.FirstOrDefault()
				: default;

	}
}
