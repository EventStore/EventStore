using System;
using EventStore.Common.Utils;
using EventStore.Transport.Http;

namespace EventStore.Core.Services.Transport.Http {
	public class ControllerAction {
		public readonly string UriTemplate;
		public readonly string HttpMethod;

		public readonly ICodec[] SupportedRequestCodecs;
		public readonly ICodec[] SupportedResponseCodecs;
		public readonly ICodec DefaultResponseCodec;

		public ControllerAction(string uriTemplate,
			string httpMethod,
			ICodec[] requestCodecs,
			ICodec[] responseCodecs) {
			Ensure.NotNull(uriTemplate, "uriTemplate");
			Ensure.NotNull(httpMethod, "httpMethod");
			Ensure.NotNull(requestCodecs, "requestCodecs");
			Ensure.NotNull(responseCodecs, "responseCodecs");

			UriTemplate = uriTemplate;
			HttpMethod = httpMethod;

			SupportedRequestCodecs = requestCodecs;
			SupportedResponseCodecs = responseCodecs;
			DefaultResponseCodec = responseCodecs.Length > 0 ? responseCodecs[0] : null;
		}

		public bool Equals(ControllerAction other) {
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return Equals(other.UriTemplate, UriTemplate) && Equals(other.HttpMethod, HttpMethod);
		}

		public override bool Equals(object obj) {
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != typeof(ControllerAction)) return false;
			return Equals((ControllerAction)obj);
		}

		public override int GetHashCode() {
			unchecked {
				return (UriTemplate.GetHashCode() * 397) ^ HttpMethod.GetHashCode();
			}
		}

		public override string ToString() {
			return string.Format("UriTemplate: {0}, HttpMethod: {1}, SupportedCodecs: {2}, DefaultCodec: {3}",
				UriTemplate,
				HttpMethod,
				SupportedResponseCodecs,
				DefaultResponseCodec);
		}
	}
}
