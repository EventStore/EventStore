using System.IO;
using Microsoft.AspNetCore.Http.Features;

namespace EventStore.Transport.Http.EntityManagement {
	public class CoreHttpResponseAdapter : IHttpResponse {
		private readonly Microsoft.AspNetCore.Http.HttpResponse _inner;

		public CoreHttpResponseAdapter(Microsoft.AspNetCore.Http.HttpResponse inner) {
			_inner = inner;
		}
		public void AddHeader(string name, string value) => _inner.Headers.Add(name, value);

		public void Close() {
			_inner.Body.Close();
		}

		public long ContentLength64 {
			get => _inner.ContentLength.GetValueOrDefault();
			set => _inner.ContentLength = value;
		}

		public string ContentType {
			get => _inner.ContentType;
			set => _inner.ContentType = value;
		}

		public Stream OutputStream => _inner.Body;

		public int StatusCode {
			get => _inner.StatusCode;
			set => _inner.StatusCode = value;
		}

		public string StatusDescription {
			get => _inner.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase;
			set => _inner.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = value;
		}
	}
}
