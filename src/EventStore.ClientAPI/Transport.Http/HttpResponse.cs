using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace EventStore.ClientAPI.Transport.Http {
	/// <summary>
	/// An Http response
	/// </summary>
	public class HttpResponse {
		/// <summary>
		/// The character set of the response.
		/// </summary>
		public readonly string CharacterSet;

		/// <summary>
		/// The character encoding of the response.
		/// </summary>
		public readonly string ContentEncoding;
		/// <summary>
		/// The Content-Length header.
		/// </summary>
		public readonly long ContentLength;
		/// <summary>
		/// The Content-Type header.
		/// </summary>
		public readonly string ContentType;

		//public readonly CookieCollection Cookies;
		/// <summary>
		/// A Collection of Http response headers.
		/// </summary>
		public readonly HttpResponseHeaders Headers;

		//public readonly bool IsFromCache;
		//public readonly bool IsMutuallyAuthenticated;//TODO TR: not implemented in mono
		//public readonly DateTime LastModified;

		/// <summary>
		/// The Http method used.
		/// </summary>
		public readonly string Method;
		//public readonly Version ProtocolVersion;

		//public readonly Uri ResponseUri;
		//public readonly string Server;

		/// <summary>
		/// The Http response's status code.
		/// </summary>
		public readonly int HttpStatusCode;
		/// <summary>
		/// The Http response's status description.
		/// </summary>
		public readonly string StatusDescription;

		/// <summary>
		/// The Http response body.
		/// </summary>
		public string Body { get; internal set; }

		/// <summary>
		/// Constructs a new <see cref="HttpResponse"/>.
		/// </summary>
		/// <param name="responseMessage"></param>
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
