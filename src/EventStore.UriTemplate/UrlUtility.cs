using System;
using System.Collections;
using System.Collections.Specialized;
using System.Runtime.Serialization;
using System.Text;

namespace EventStore.UriTemplate
{
	//copied from System.Web.HttpUtility code (renamed here) to remove dependency on System.Web.dll
	internal static class UrlUtility {
		//  Query string parsing support
		public static NameValueCollection ParseQueryString(string query) {
			return ParseQueryString(query, Encoding.UTF8);
		}

		public static NameValueCollection ParseQueryString(string query, Encoding encoding) {
			Ensure.NotNull(query, nameof(query));
			Ensure.NotNull(encoding, nameof(encoding));

			if (query.Length > 0 && query[0] == '?') {
				query = query.Substring(1);
			}

			return new HttpValueCollection(query, encoding);
		}

		public static string UrlEncode(string str) {
			if (str == null) {
				return null;
			}
			return UrlEncode(str, Encoding.UTF8);
		}

		// URL encodes a path portion of a URL string and returns the encoded string.
		public static string UrlPathEncode(string str) {
			if (str == null) {
				return null;
			}

			// recurse in case there is a query string
			var i = str.IndexOf('?');
			if (i >= 0) {
				return UrlPathEncode(str.Substring(0, i)) + str.Substring(i);
			}

			// encode DBCS characters and spaces only
			return UrlEncodeSpaces(UrlEncodeNonAscii(str, Encoding.UTF8));
		}

		public static string UrlEncode(string str, Encoding encoding) {
			if (str == null) {
				return null;
			}
			return Encoding.ASCII.GetString(UrlEncodeToBytes(str, encoding));
		}

		public static string UrlEncodeUnicode(string str) {
			if (str == null)
				return null;
			return UrlEncodeUnicodeStringToStringInternal(str, false);

		}

		private static string UrlEncodeUnicodeStringToStringInternal(string s, bool ignoreAscii) {
			var l = s.Length;
			var sb = new StringBuilder(l);

			for (var i = 0; i < l; i++) {
				var ch = s[i];

				if ((ch & 0xff80) == 0) {  // 7 bit?
					if (ignoreAscii || IsSafe(ch)) {
						sb.Append(ch);
					} else if (ch == ' ') {
						sb.Append('+');
					} else {
						sb.Append('%');
						sb.Append(IntToHex((ch >> 4) & 0xf));
						sb.Append(IntToHex((ch) & 0xf));
					}
				} else { // arbitrary Unicode?
					sb.Append("%u");
					sb.Append(IntToHex((ch >> 12) & 0xf));
					sb.Append(IntToHex((ch >> 8) & 0xf));
					sb.Append(IntToHex((ch >> 4) & 0xf));
					sb.Append(IntToHex((ch) & 0xf));
				}
			}

			return sb.ToString();
		}

		//  Helper to encode the non-ASCII url characters only
		private static string UrlEncodeNonAscii(string str, Encoding e) {
			if (string.IsNullOrEmpty(str)) {
				return str;
			}
			if (e == null) {
				e = Encoding.UTF8;
			}
			var bytes = e.GetBytes(str);
			bytes = UrlEncodeBytesToBytesInternalNonAscii(bytes, 0, bytes.Length, false);
			return Encoding.ASCII.GetString(bytes);
		}

		//  Helper to encode spaces only
		private static string UrlEncodeSpaces(string str) {
			if (str != null && str.IndexOf(' ') >= 0) {
				str = str.Replace(" ", "%20");
			}
			return str;
		}

		public static byte[] UrlEncodeToBytes(string str, Encoding e) {
			if (str == null) {
				return null;
			}
			var bytes = e.GetBytes(str);
			return UrlEncodeBytesToBytesInternal(bytes, 0, bytes.Length, false);
		}

		//public static string UrlDecode(string str)
		//{
		//    if (str == null)
		//        return null;
		//    return UrlDecode(str, Encoding.UTF8);
		//}

		public static string UrlDecode(string str, Encoding e) {
			if (str == null) {
				return null;
			}
			return UrlDecodeStringFromStringInternal(str, e);
		}

		//  Implementation for encoding
		private static byte[] UrlEncodeBytesToBytesInternal(byte[] bytes, int offset, int count, bool alwaysCreateReturnValue) {
			var cSpaces = 0;
			var cUnsafe = 0;

			// count them first
			for (var i = 0; i < count; i++) {
				var ch = (char)bytes[offset + i];

				if (ch == ' ') {
					cSpaces++;
				} else if (!IsSafe(ch)) {
					cUnsafe++;
				}
			}

			// nothing to expand?
			if (!alwaysCreateReturnValue && cSpaces == 0 && cUnsafe == 0) {
				return bytes;
			}

			// expand not 'safe' characters into %XX, spaces to +s
			var expandedBytes = new byte[count + cUnsafe * 2];
			var pos = 0;

			for (var i = 0; i < count; i++) {
				var b = bytes[offset + i];
				var ch = (char)b;

				if (IsSafe(ch)) {
					expandedBytes[pos++] = b;
				} else if (ch == ' ') {
					expandedBytes[pos++] = (byte)'+';
				} else {
					expandedBytes[pos++] = (byte)'%';
					expandedBytes[pos++] = (byte)IntToHex((b >> 4) & 0xf);
					expandedBytes[pos++] = (byte)IntToHex(b & 0x0f);
				}
			}

			return expandedBytes;
		}


		private static bool IsNonAsciiByte(byte b) {
			return (b >= 0x7F || b < 0x20);
		}

		private static byte[] UrlEncodeBytesToBytesInternalNonAscii(byte[] bytes, int offset, int count, bool alwaysCreateReturnValue) {
			var cNonAscii = 0;

			// count them first
			for (var i = 0; i < count; i++) {
				if (IsNonAsciiByte(bytes[offset + i])) {
					cNonAscii++;
				}
			}

			// nothing to expand?
			if (!alwaysCreateReturnValue && cNonAscii == 0) {
				return bytes;
			}

			// expand not 'safe' characters into %XX, spaces to +s
			var expandedBytes = new byte[count + cNonAscii * 2];
			var pos = 0;

			for (var i = 0; i < count; i++) {
				var b = bytes[offset + i];

				if (IsNonAsciiByte(b)) {
					expandedBytes[pos++] = (byte)'%';
					expandedBytes[pos++] = (byte)IntToHex((b >> 4) & 0xf);
					expandedBytes[pos++] = (byte)IntToHex(b & 0x0f);
				} else {
					expandedBytes[pos++] = b;
				}
			}

			return expandedBytes;
		}

		private static string UrlDecodeStringFromStringInternal(string s, Encoding e) {
			var count = s.Length;
			var helper = new UrlDecoder(count, e);

			// go through the string's chars collapsing %XX and %uXXXX and
			// appending each char as char, with exception of %XX constructs
			// that are appended as bytes

			for (var pos = 0; pos < count; pos++) {
				var ch = s[pos];

				if (ch == '+') {
					ch = ' ';
				} else if (ch == '%' && pos < count - 2) {
					if (s[pos + 1] == 'u' && pos < count - 5) {
						var h1 = HexToInt(s[pos + 2]);
						var h2 = HexToInt(s[pos + 3]);
						var h3 = HexToInt(s[pos + 4]);
						var h4 = HexToInt(s[pos + 5]);

						if (h1 >= 0 && h2 >= 0 && h3 >= 0 && h4 >= 0) {   // valid 4 hex chars
							ch = (char)((h1 << 12) | (h2 << 8) | (h3 << 4) | h4);
							pos += 5;

							// only add as char
							helper.AddChar(ch);
							continue;
						}
					} else {
						var h1 = HexToInt(s[pos + 1]);
						var h2 = HexToInt(s[pos + 2]);

						if (h1 >= 0 && h2 >= 0) {     // valid 2 hex chars
							var b = (byte)((h1 << 4) | h2);
							pos += 2;

							// don't add as char
							helper.AddByte(b);
							continue;
						}
					}
				}

				if ((ch & 0xFF80) == 0) {
					helper.AddByte((byte)ch); // 7 bit have to go as bytes because of Unicode
				} else {
					helper.AddChar(ch);
				}
			}

			return helper.GetString();
		}

		// Private helpers for URL encoding/decoding
		private static int HexToInt(char h) {
			return (h >= '0' && h <= '9') ? h - '0' :
			(h >= 'a' && h <= 'f') ? h - 'a' + 10 :
			(h >= 'A' && h <= 'F') ? h - 'A' + 10 :
			-1;
		}

		private static char IntToHex(int n) {
			//WCF CHANGE: CHANGED FROM Debug.Assert() to Fx.Assert()
			if (n < 0x10 == false) throw new Exception("n < 0x10");

			if (n <= 9) {
				return (char)(n + (int)'0');
			} else {
				return (char)(n - 10 + (int)'a');
			}
		}

		// Set of safe chars, from RFC 1738.4 minus '+'
		internal static bool IsSafe(char ch) {
			if (ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9') {
				return true;
			}

			switch (ch) {
				case '-':
				case '_':
				case '.':
				case '!':
				case '*':
				case '\'':
				case '(':
				case ')':
					return true;
			}

			return false;
		}

		// Internal class to facilitate URL decoding -- keeps char buffer and byte buffer, allows appending of either chars or bytes
		private class UrlDecoder {
			private int _bufferSize;

			// Accumulate characters in a special array
			private int _numChars;
			private char[] _charBuffer;

			// Accumulate bytes for decoding into characters in a special array
			private int _numBytes;
			private byte[] _byteBuffer;

			// Encoding to convert chars to bytes
			private Encoding _encoding;

			private void FlushBytes() {
				if (_numBytes > 0) {
					_numChars += _encoding.GetChars(_byteBuffer, 0, _numBytes, _charBuffer, _numChars);
					_numBytes = 0;
				}
			}

			internal UrlDecoder(int bufferSize, Encoding encoding) {
				_bufferSize = bufferSize;
				_encoding = encoding;

				_charBuffer = new char[bufferSize];
				// byte buffer created on demand
			}

			internal void AddChar(char ch) {
				if (_numBytes > 0) {
					FlushBytes();
				}

				_charBuffer[_numChars++] = ch;
			}

			internal void AddByte(byte b) {
				// if there are no pending bytes treat 7 bit bytes as characters
				// this optimization is temp disable as it doesn't work for some encodings

				//if (_numBytes == 0 && ((b & 0x80) == 0)) {
				//    AddChar((char)b);
				//}
				//else

				{
					if (_byteBuffer == null) {
						_byteBuffer = new byte[_bufferSize];
					}

					_byteBuffer[_numBytes++] = b;
				}
			}

			internal string GetString() {
				if (_numBytes > 0) {
					FlushBytes();
				}

				if (_numChars > 0) {
					return new String(_charBuffer, 0, _numChars);
				} else {
					return string.Empty;
				}
			}
		}

		[Serializable]
		private class HttpValueCollection : NameValueCollection {
			internal HttpValueCollection(string str, Encoding encoding)
				: base(StringComparer.OrdinalIgnoreCase) {
				if (!string.IsNullOrEmpty(str)) {
					FillFromString(str, true, encoding);
				}

				IsReadOnly = false;
			}

			protected HttpValueCollection(SerializationInfo info, StreamingContext context)
				: base(info, context) {
			}

			internal void FillFromString(string s, bool urlencoded, Encoding encoding) {
				var l = (s != null) ? s.Length : 0;
				var i = 0;

				while (i < l) {
					// find next & while noting first = on the way (and if there are more)

					var si = i;
					var ti = -1;

					while (i < l) {
						var ch = s[i];

						if (ch == '=') {
							if (ti < 0)
								ti = i;
						} else if (ch == '&') {
							break;
						}

						i++;
					}

					// extract the name / value pair

					string name = null;
					string value = null;

					if (ti >= 0) {
						name = s.Substring(si, ti - si);
						value = s.Substring(ti + 1, i - ti - 1);
					} else {
						value = s.Substring(si, i - si);
					}

					// add name / value pair to the collection

					if (urlencoded) {
						base.Add(
						   UrlDecode(name, encoding),
						   UrlDecode(value, encoding));
					} else {
						base.Add(name, value);
					}

					// trailing '&'

					if (i == l - 1 && s[i] == '&') {
						base.Add(null, string.Empty);
					}

					i++;
				}
			}

			public override string ToString() {
				return ToString(true, null);
			}

			private string ToString(bool urlencoded, IDictionary excludeKeys) {
				var n = Count;
				if (n == 0)
					return string.Empty;

				var s = new StringBuilder();
				string key, keyPrefix, item;

				for (var i = 0; i < n; i++) {
					key = GetKey(i);

					if (excludeKeys != null && key != null && excludeKeys[key] != null) {
						continue;
					}
					if (urlencoded) {
						key = UrlEncodeUnicode(key);
					}
					keyPrefix = (!string.IsNullOrEmpty(key)) ? (key + "=") : string.Empty;

					var values = (ArrayList)BaseGet(i);
					var numValues = (values != null) ? values.Count : 0;

					if (s.Length > 0) {
						s.Append('&');
					}

					if (numValues == 1) {
						s.Append(keyPrefix);
						item = (string)values[0];
						if (urlencoded)
							item = UrlEncodeUnicode(item);
						s.Append(item);
					} else if (numValues == 0) {
						s.Append(keyPrefix);
					} else {
						for (var j = 0; j < numValues; j++) {
							if (j > 0) {
								s.Append('&');
							}
							s.Append(keyPrefix);
							item = (string)values[j];
							if (urlencoded) {
								item = UrlEncodeUnicode(item);
							}
							s.Append(item);
						}
					}
				}

				return s.ToString();
			}
		}
	}
}
