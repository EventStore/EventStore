using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace EventStore.UriTemplate {
	internal class HttpHeadersWebHeaderCollection : WebHeaderCollection {
		private const string HasKeysHeader = "hk";
		private static readonly string[] EmptyStringArray = new string[] { string.Empty };
		private static readonly string[] StringSplitArray = new string[] { ", " };

		// Cloned from WebHeaderCollection
		private static readonly char[] HttpTrimCharacters = new char[] { (char)0x09, (char)0xA, (char)0xB, (char)0xC, (char)0xD, (char)0x20 };
		private static readonly char[] InvalidParamChars = new char[] { '(', ')', '<', '>', '@', ',', ';', ':', '\\', '"', '\'', '/', '[', ']', '?', '=', '{', '}', ' ', '\t', '\r', '\n' };

		private readonly HttpRequestMessage _httpRequestMessage;
		private readonly HttpResponseMessage _httpResponseMessage;
		private bool _hasKeys;

		public HttpHeadersWebHeaderCollection(HttpRequestMessage httpRequestMessage) {
			_httpRequestMessage = httpRequestMessage ?? throw new ArgumentNullException(nameof(httpRequestMessage));
			EnsureBaseHasKeysIsAccurate();
		}

		public HttpHeadersWebHeaderCollection(HttpResponseMessage httpResponseMessage) {
			_httpResponseMessage = httpResponseMessage ?? throw new ArgumentNullException(nameof(_httpRequestMessage));
			EnsureBaseHasKeysIsAccurate();
		}

		public override string[] AllKeys {
			get {
				return AllHeaders.Select(header => header.Key).ToArray();
			}
		}

		public override int Count {
			get {
				return AllHeaders.Count();
			}
		}

		public override KeysCollection Keys {
			get {
				// The perf here will be awful as we have to create a NameValueCollection and copy all the
				// headers over into it in order to get an instance of type KeysCollection; so framework
				// code should never use the Keys property.
				var collection = new NameValueCollection();
				foreach (var header in AllHeaders) {
					var values = header.Value.ToArray();
					if (values.Length == 0) {
						collection.Add(header.Key, string.Empty);
					} else {
						foreach (var value in values) {
							collection.Add(header.Key, value);
						}
					}
				}

				return collection.Keys;
			}
		}

		private IEnumerable<KeyValuePair<string, IEnumerable<string>>> AllHeaders {
			get {
				HttpContent content = null;
				IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers;

				if (_httpRequestMessage != null) {
					headers = _httpRequestMessage.Headers;
					content = _httpRequestMessage.Content;
				} else {
					if (_httpRequestMessage == null) {
						throw new ArgumentNullException(nameof(_httpRequestMessage));
					}
					headers = _httpResponseMessage.Headers;
					content = _httpResponseMessage.Content;
				}

				if (content != null) {
					headers = headers.Concat(content.Headers);
				}

				return headers;
			}
		}

		public override void Add(string name, string value) {
			name = CheckBadChars(name, false);
			value = CheckBadChars(value, true);

			if (_httpRequestMessage != null) {
				_httpRequestMessage.Headers.Add(name, value);
			} else {
				if (_httpResponseMessage == null) {
					throw new ArgumentNullException(nameof(_httpResponseMessage));
				}
				_httpResponseMessage.Headers.Add(name, value);
			}

			EnsureBaseHasKeysIsAccurate();
		}

		public override void Clear() {
			HttpContent content;

			if (_httpRequestMessage != null) {
				_httpRequestMessage.Headers.Clear();
				content = _httpRequestMessage.Content;
			} else {
				if (_httpResponseMessage == null) {
					throw new ArgumentNullException(nameof(_httpResponseMessage));
				}
				_httpResponseMessage.Headers.Clear();
				content = _httpResponseMessage.Content;
			}

			content?.Headers.Clear();

			EnsureBaseHasKeysIsAccurate();
		}

		public override void Remove(string name) {
			name = CheckBadChars(name, false);

			if (_httpRequestMessage != null) {
				_httpRequestMessage.Headers.Remove(name);
			} else {
				_httpResponseMessage.Headers.Remove(name);
			}

			EnsureBaseHasKeysIsAccurate();
		}

		public override void Set(string name, string value) {
			name = CheckBadChars(name, false);
			value = CheckBadChars(value, true);

			if (_httpRequestMessage != null) {
				_httpRequestMessage.Headers.Add(name, value); //.SetHeader(name, value);
			} else {
				if (_httpResponseMessage == null) {
					throw new ArgumentNullException(nameof(_httpResponseMessage));
				}
				_httpResponseMessage.Headers.Add(name, value); //.SetHeader(name, value);
			}

			EnsureBaseHasKeysIsAccurate();
		}

		public override IEnumerator GetEnumerator() {
			return new HttpHeadersEnumerator(AllKeys);
		}

		public override string Get(int index) {
			var values = GetValues(index);
			return GetSingleValue(values);
		}

		public override string GetKey(int index) {
			return GetHeaderAt(index).Key;
		}

		public override string[] GetValues(int index) {
			return GetHeaderAt(index).Value.ToArray();
		}

		public override string Get(string name) {
			var values = GetValues(name);
			return GetSingleValue(values);
		}

		public override string ToString() {
			var builder = new StringBuilder();
			foreach (var header in AllHeaders) {
				if (string.IsNullOrEmpty(header.Key)) continue;
				builder.Append(header.Key);
				builder.Append(": ");
				builder.AppendLine(GetSingleValue(header.Value.ToArray()));
			}

			return builder.ToString();
		}

		public override string[] GetValues(string header) {
			IEnumerable<string> values;

			if (_httpRequestMessage != null) {
				values = _httpRequestMessage.Headers.GetValues(header);
			} else {
				if (_httpResponseMessage == null) {
					throw new ArgumentNullException(nameof(_httpResponseMessage));
				}
				values = _httpResponseMessage.Headers.GetValues(header);
			}

			if (values == null) {
				return EmptyStringArray;
			}

			return values.SelectMany(str => str.Split(StringSplitArray, StringSplitOptions.None)).ToArray();
		}

		private static string GetSingleValue(string[] values) {
			if (values == null) {
				return null;
			}

			if (values.Length == 1) {
				return values[0];
			}

			// The current implemenation of the base WebHeaderCollection joins the string values
			// using a comma with no whitespace
			return string.Join(",", values);
		}

		// Cloned from WebHeaderCollection
		//[System.Diagnostics.CodeAnalysis.SuppressMessage(FxCop.Category.ReliabilityBasic, FxCop.Rule.WrapExceptionsRule,
		//	Justification = "This code is being used to reproduce behavior from the WebHeaderCollection, which does not trace exceptions via FxTrace.")]
		private static string CheckBadChars(string name, bool isHeaderValue) {
			if (string.IsNullOrEmpty(name)) {
				if (!isHeaderValue) {
					throw name == null ?
						new ArgumentNullException(nameof(name)) :
						new ArgumentException(nameof(name));
				}

				// empty value is OK
				return string.Empty;
			}

			if (isHeaderValue) {
				// VALUE check
				// Trim spaces from both ends
				name = name.Trim(HttpTrimCharacters);

				// First, check for correctly formed multi-line value
				// Second, check for absence of CTL characters
				var crlf = 0;
				for (var i = 0; i < name.Length; ++i) {
					var c = (char)(0x000000ff & (uint)name[i]);
					switch (crlf) {
						case 0:
							if (c == '\r') {
								crlf = 1;
							} else if (c == '\n') {
								// Technically this is bad HTTP.  But it would be a breaking change to throw here.
								// Is there an exploit?
								crlf = 2;
							} else if (c == 127 || c < ' ' && c != '\t') {
								throw new ArgumentException( "value check");
							}

							break;

						case 1:
							if (c == '\n') {
								crlf = 2;
								break;
							}

							throw new ArgumentException("WebHeaderInvalidCRLFChars value");

						case 2:
							if (c == ' ' || c == '\t') {
								crlf = 0;
								break;
							}

							throw new ArgumentException("WebHeaderInvalidCRLFChars value");
					}
				}

				if (crlf != 0) {
					throw new ArgumentException("WebHeaderInvalidCRLFChars value");
				}
			} else {
				// NAME check
				// First, check for absence of separators and spaces
				if (name.IndexOfAny(InvalidParamChars) != -1) {
					throw new ArgumentException("WebHeaderInvalidHeaderChars name");
				}

				// Second, check for non CTL ASCII-7 characters (32-126)
				if (ContainsNonAsciiChars(name)) {
					throw new ArgumentException("WebHeaderInvalidNonAsciiChars name");
				}
			}

			return name;
		}

		// Cloned from WebHeaderCollection
		private static bool ContainsNonAsciiChars(string token) {
			for (var i = 0; i < token.Length; ++i) {
				if (token[i] < 0x20 || token[i] > 0x7e) {
					return true;
				}
			}

			return false;
		}

		private void EnsureBaseHasKeysIsAccurate() {
			var originalHasKeys = _hasKeys;
			_hasKeys = BackingHttpHeadersHasKeys();
			if (originalHasKeys && !_hasKeys) {
				base.Remove(HasKeysHeader);
			} else if (!originalHasKeys && _hasKeys) {
				AddWithoutValidate(HasKeysHeader, string.Empty);
			}
		}

		private bool BackingHttpHeadersHasKeys() {
			return _httpRequestMessage != null ?
				_httpRequestMessage.Headers.Any() || _httpRequestMessage.Content != null && _httpRequestMessage.Content.Headers.Any() :
				_httpResponseMessage.Headers.Any() || _httpResponseMessage.Content != null && _httpResponseMessage.Content.Headers.Any();
		}

		private KeyValuePair<string, IEnumerable<string>> GetHeaderAt(int index) {
			if (index >= 0) {
				foreach (var header in AllHeaders) {
					if (index == 0) {
						return header;
					}

					index--;
				}
			}

			throw new ArgumentOutOfRangeException("WebHeaderArgumentOutOfRange index");
		}

		private class HttpHeadersEnumerator : IEnumerator {
			private readonly string[] _keys;
			private int _position;

			public HttpHeadersEnumerator(string[] keys) {
				_keys = keys;
				_position = -1;
			}

			public object Current {
				get {
					if (_position < 0 || _position >= _keys.Length) {
						throw new InvalidOperationException("WebHeaderEnumOperationCantHappen");
					}

					return _keys[_position];
				}
			}

			public bool MoveNext() {
				if (_position < _keys.Length - 1) {
					_position++;
					return true;
				}

				_position = _keys.Length;
				return false;
			}

			public void Reset() {
				_position = -1;
			}
		}
	}
}
