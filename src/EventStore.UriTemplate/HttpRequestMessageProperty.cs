using System;
using System.Net;
using System.Net.Http;

namespace EventStore.UriTemplate {
	public sealed class HttpRequestMessageProperty : IMessageProperty, IMergeEnabledMessageProperty {
		private TraditionalHttpRequestMessageProperty _traditionalProperty;
		private HttpRequestMessageBackedProperty _httpBackedProperty;
		private bool _initialCopyPerformed;
		private bool _useHttpBackedProperty;

		public HttpRequestMessageProperty()
			: this((IHttpHeaderProvider)null) {
		}

		internal HttpRequestMessageProperty(IHttpHeaderProvider httpHeaderProvider) {
			_traditionalProperty = new TraditionalHttpRequestMessageProperty(httpHeaderProvider);
			_useHttpBackedProperty = false;
		}

		internal HttpRequestMessageProperty(HttpRequestMessage httpRequestMessage) {
			_httpBackedProperty = new HttpRequestMessageBackedProperty(httpRequestMessage);
			_useHttpBackedProperty = true;
		}

		public static string Name {
			get { return "httpRequest"; }
		}

		public WebHeaderCollection Headers {
			get {
				return _useHttpBackedProperty ?
					_httpBackedProperty.Headers :
					_traditionalProperty.Headers;
			}
		}

		public string Method {
			get {
				return _useHttpBackedProperty ?
					_httpBackedProperty.Method :
					_traditionalProperty.Method;
			}

			set {
				if (value == null) {
					throw new ArgumentNullException(nameof(value));
				}

				if (_useHttpBackedProperty) {
					_httpBackedProperty.Method = value;
				} else {
					_traditionalProperty.Method = value;
				}
			}
		}

		public string QueryString {
			get {
				return _useHttpBackedProperty ?
					_httpBackedProperty.QueryString :
					_traditionalProperty.QueryString;
			}

			set {

				if (value == null) {
					throw new ArgumentNullException(nameof(value));
				}

				if (_useHttpBackedProperty) {
					_httpBackedProperty.QueryString = value;
				} else {
					_traditionalProperty.QueryString = value;
				}
			}
		}

		public bool SuppressEntityBody {
			get {
				return _useHttpBackedProperty ?
					_httpBackedProperty.SuppressEntityBody :
					_traditionalProperty.SuppressEntityBody;
			}

			set {
				if (_useHttpBackedProperty) {
					_httpBackedProperty.SuppressEntityBody = value;
				} else {
					_traditionalProperty.SuppressEntityBody = value;
				}
			}
		}

		//private HttpRequestMessage HttpRequestMessage {
		//	get {
		//		if (_useHttpBackedProperty) {
		//			return _httpBackedProperty.HttpRequestMessage;
		//		}

		//		return null;
		//	}
		//}

		//internal static HttpRequestMessage GetHttpRequestMessageFromMessage(Message message) {
		//	HttpRequestMessage httpRequestMessage = null;

		//	HttpRequestMessageProperty property = message.Properties.GetValue<HttpRequestMessageProperty>(HttpRequestMessageProperty.Name);
		//	if (property != null) {
		//		httpRequestMessage = property.HttpRequestMessage;
		//		if (httpRequestMessage != null) {
		//			httpRequestMessage.CopyPropertiesFromMessage(message);
		//			message.EnsureReadMessageState();
		//		}
		//	}

		//	return httpRequestMessage;
		//}

		IMessageProperty IMessageProperty.CreateCopy() {
			if (!_useHttpBackedProperty ||
				!_initialCopyPerformed) {
				_initialCopyPerformed = true;
				return this;
			}

			return _httpBackedProperty.CreateTraditionalRequestMessageProperty();
		}

		bool IMergeEnabledMessageProperty.TryMergeWithProperty(object propertyToMerge) {
			// The ImmutableDispatchRuntime will merge MessageProperty instances from the
			//  OperationContext (that were created before the response message was created) with
			//  MessageProperty instances on the message itself.  The message's version of the 
			//  HttpRequestMessageProperty may hold a reference to an HttpRequestMessage, and this 
			//  cannot be discarded, so values from the OperationContext's property must be set on 
			//  the message's version without completely replacing the message's property.
			if (_useHttpBackedProperty) {
				var requestProperty = propertyToMerge as HttpRequestMessageProperty;
				if (requestProperty != null) {
					if (!requestProperty._useHttpBackedProperty) {
						_httpBackedProperty.MergeWithTraditionalProperty(requestProperty._traditionalProperty);
						requestProperty._traditionalProperty = null;
						requestProperty._httpBackedProperty = _httpBackedProperty;
						requestProperty._useHttpBackedProperty = true;
					}

					return true;
				}
			}

			return false;
		}

		internal interface IHttpHeaderProvider {
			void CopyHeaders(WebHeaderCollection headers);
		}

		private class TraditionalHttpRequestMessageProperty {
			public const string DefaultMethod = "POST";
			public const string DefaultQueryString = "";

			private WebHeaderCollection _headers;
			private IHttpHeaderProvider _httpHeaderProvider;
			private string _method;

			public TraditionalHttpRequestMessageProperty(IHttpHeaderProvider httpHeaderProvider) {
				_httpHeaderProvider = httpHeaderProvider;
				_method = DefaultMethod;
				QueryString = DefaultQueryString;
			}

			public WebHeaderCollection Headers {
				get {
					if (_headers == null) {
						_headers = new WebHeaderCollection();
						if (_httpHeaderProvider != null) {
							_httpHeaderProvider.CopyHeaders(_headers);
							_httpHeaderProvider = null;
						}
					}

					return _headers;
				}
			}

			public string Method {
				get {
					return _method;
				}

				set {
					_method = value;
					HasMethodBeenSet = true;
				}
			}

			public bool HasMethodBeenSet { get; private set; }

			public string QueryString { get; set; }

			public bool SuppressEntityBody { get; set; }
		}

		private class HttpRequestMessageBackedProperty {
			private HttpHeadersWebHeaderCollection _headers;

			public HttpRequestMessageBackedProperty(HttpRequestMessage httpRequestMessage) {
				HttpRequestMessage = httpRequestMessage ?? throw new ArgumentNullException(nameof(httpRequestMessage));
			}

			public HttpRequestMessage HttpRequestMessage { get; private set; }

			public WebHeaderCollection Headers {
				get {
					if (_headers == null) {
						_headers = new HttpHeadersWebHeaderCollection(HttpRequestMessage);
					}

					return _headers;
				}
			}

			public string Method {
				get {
					return HttpRequestMessage.Method.Method;
				}

				set {
					HttpRequestMessage.Method = new HttpMethod(value);
				}
			}

			public string QueryString {
				get {
					var query = HttpRequestMessage.RequestUri.Query;
					return query.Length > 0 ? query.Substring(1) : string.Empty;
				}

				set {
					var uriBuilder = new UriBuilder(HttpRequestMessage.RequestUri);
					uriBuilder.Query = value;
					HttpRequestMessage.RequestUri = uriBuilder.Uri;
				}
			}

			public bool SuppressEntityBody {
				get {
					var content = HttpRequestMessage.Content;
					if (content != null) {
						var contentLength = content.Headers.ContentLength;

						if (!contentLength.HasValue || contentLength.Value > 0) {
							return false;
						}
					}

					return true;
				}
				set {
					var content = HttpRequestMessage.Content;
					if (value && content != null &&
						(!content.Headers.ContentLength.HasValue ||
						content.Headers.ContentLength.Value > 0)) {
						HttpContent newContent = new ByteArrayContent(EmptyArray<byte>.Instance);
						foreach (var header in content.Headers) {
							newContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
						}

						HttpRequestMessage.Content = newContent;
						content.Dispose();
					} else if (!value && content == null) {
						HttpRequestMessage.Content = new ByteArrayContent(EmptyArray<byte>.Instance);
					}
				}
			}

			public HttpRequestMessageProperty CreateTraditionalRequestMessageProperty() {
				var copiedProperty = new HttpRequestMessageProperty();

				copiedProperty.Headers.Add(Headers);

				if (Method != TraditionalHttpRequestMessageProperty.DefaultMethod) {
					copiedProperty.Method = Method;
				}

				copiedProperty.QueryString = QueryString;
				copiedProperty.SuppressEntityBody = SuppressEntityBody;

				return copiedProperty;
			}

			public void MergeWithTraditionalProperty(TraditionalHttpRequestMessageProperty propertyToMerge) {
				if (propertyToMerge.HasMethodBeenSet) {
					Method = propertyToMerge.Method;
				}

				if (propertyToMerge.QueryString != TraditionalHttpRequestMessageProperty.DefaultQueryString) {
					QueryString = propertyToMerge.QueryString;
				}

				SuppressEntityBody = propertyToMerge.SuppressEntityBody;

				var headersToMerge = propertyToMerge.Headers;
				foreach (var headerKey in headersToMerge.AllKeys) {
					Headers[headerKey] = headersToMerge[headerKey];
				}
			}
		}
	}
}
