using System.Net;

namespace EventStore.Common.Utils {
	public static class EndpointExtensions {
		public const string HTTP_SCHEMA = "http";
		public const string HTTPS_SCHEMA = "https";

		public static string ToHttpUrl(this EndPoint endPoint, string schema, string rawUrl = null) {
			if (endPoint is IPEndPoint) {
				var ipEndPoint = endPoint as IPEndPoint;
				return CreateHttpUrl(schema, ipEndPoint.Address.ToString(), ipEndPoint.Port,
					rawUrl != null ? rawUrl.TrimStart('/') : string.Empty);
			}

			if (endPoint is DnsEndPoint) {
				var dnsEndpoint = endPoint as DnsEndPoint;
				return CreateHttpUrl(schema, dnsEndpoint.Host, dnsEndpoint.Port,
					rawUrl != null ? rawUrl.TrimStart('/') : string.Empty);
			}

			return null;
		}

		public static string ToHttpUrl(this EndPoint endPoint, string schema, string formatString,
			params object[] args) {
			if (endPoint is IPEndPoint) {
				var ipEndPoint = endPoint as IPEndPoint;
				return CreateHttpUrl(schema, ipEndPoint.Address.ToString(), ipEndPoint.Port,
					string.Format(formatString.TrimStart('/'), args));
			}

			if (endPoint is DnsEndPoint) {
				var dnsEndpoint = endPoint as DnsEndPoint;
				return CreateHttpUrl(schema, dnsEndpoint.Host, dnsEndpoint.Port,
					string.Format(formatString.TrimStart('/'), args));
			}

			return null;
		}

		private static string CreateHttpUrl(string schema, string host, int port, string path) {
			return $"{schema}://{host}:{port}/{path}";
		}
	}
}
