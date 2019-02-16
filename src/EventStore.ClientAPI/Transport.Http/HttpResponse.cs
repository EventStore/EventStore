using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace EventStore.ClientAPI.Transport.Http {
	internal class HttpResponse {
		public readonly string CharacterSet;

		public readonly string ContentEncoding;
		public readonly long ContentLength;
		public readonly string ContentType;

		//public readonly CookieCollection Cookies;
		public readonly HttpResponseHeaders Headers;

		//public readonly bool IsFromCache;
		//public readonly bool IsMutuallyAuthenticated;//TODO TR: not implemented in mono
		//public readonly DateTime LastModified;

		public readonly string Method;
		//public readonly Version ProtocolVersion;

		//public readonly Uri ResponseUri;
		//public readonly string Server;

		public readonly int HttpStatusCode;
		public readonly string StatusDescription;

		public string Body { get; internal set; }

		public HttpResponse(HttpResponseMessage responseMessage) {
			ContentEncoding = responseMessage.Content.Headers.ContentEncoding.FirstOrDefault();
			ContentLength = responseMessage.Content.Headers.ContentLength.Value;

			if (responseMessage.Content.Headers.ContentType != null) {
				CharacterSet = responseMessage.Content.Headers.ContentType.CharSet;
				ContentType = responseMessage.Content.Headers.ContentType.MediaType;
			}


			//Cookies = httpWebResponse.Cookies;
			Headers = responseMessage.Headers;

			//IsFromCache = httpWebResponse.IsFromCache;
			//IsMutuallyAuthenticated = httpWebResponse.IsMutuallyAuthenticated;

			//LastModified = httpWebResponse.LastModified;

			Method = responseMessage.RequestMessage.Method.ToString();
			//ProtocolVersion = httpWebResponse.ProtocolVersion;

			//ResponseUri = httpWebResponse.ResponseUri;
			//Server = httpWebResponse.Server;

			HttpStatusCode = (int)responseMessage.StatusCode;
			StatusDescription = responseMessage.ReasonPhrase;
		}
	}
}
