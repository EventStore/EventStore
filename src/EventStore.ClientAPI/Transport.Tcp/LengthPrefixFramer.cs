using System;
using System.Collections.Generic;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.Transport.Tcp {
	internal class LengthPrefixMessageFramer {
		private readonly int _maxPackageSize;
		public const int HeaderLength = sizeof(Int32);

		private byte[] _messageBuffer;
		private int _bufferIndex;
		private Action<ArraySegment<byte>> _receivedHandler;

		private int _headerBytes;
		private int _packageLength;

		/// <summary>
		/// Initializes a new instance of the <see cref="LengthPrefixMessageFramer"/> class.
		/// </summary>
		public LengthPrefixMessageFramer(int maxPackageSize = 64 * 1024 * 1024) {
			Ensure.Positive(maxPackageSize, "maxPackageSize");
			_maxPackageSize = maxPackageSize;
		}

		public void Reset() {
			_messageBuffer = null;
			_headerBytes = 0;
			_packageLength = 0;
			_bufferIndex = 0;
		}

		public void UnFrameData(IEnumerable<ArraySegment<byte>> data) {
			if (data == null)
				throw new ArgumentNullException("data");

			foreach (ArraySegment<byte> buffer in data) {
				Parse(buffer);
			}
		}

		public void UnFrameData(ArraySegment<byte> data) {
			Parse(data);
		}

		/// <summary>
		/// Parses a stream chunking based on length-prefixed framing. Calls are re-entrant and hold state internally.
		/// </summary>
		/// <param name="bytes">A byte array of data to append</param>
		private void Parse(ArraySegment<byte> bytes) {
			byte[] data = bytes.Array;
			for (int i = bytes.Offset; i < bytes.Offset + bytes.Count; i++) {
				if (_headerBytes < HeaderLength) {
					_packageLength |= (data[i] << (_headerBytes * 8)); // little-endian order
					++_headerBytes;
					if (_headerBytes != HeaderLength) continue;
					if (_packageLength <= 0 || _packageLength > _maxPackageSize)
						throw new PackageFramingException(string.Format(
							"Package size is out of bounds: {0} (max: {1}). This is likely an exceptionally large message (reading too many things) or there is a problem with the framing if working on a new client.",
							_packageLength, _maxPackageSize));

					_messageBuffer = new byte[_packageLength];
				} else {
					int copyCnt = Math.Min(bytes.Count + bytes.Offset - i, _packageLength - _bufferIndex);
					Buffer.BlockCopy(bytes.Array, i, _messageBuffer, _bufferIndex, copyCnt);
					_bufferIndex += copyCnt;
					i += copyCnt - 1;

					if (_bufferIndex == _packageLength) {
						if (_receivedHandler != null)
							_receivedHandler(new ArraySegment<byte>(_messageBuffer, 0, _bufferIndex));
						_messageBuffer = null;
						_headerBytes = 0;
						_packageLength = 0;
						_bufferIndex = 0;
					}
				}
			}
		}

		public IEnumerable<ArraySegment<byte>> FrameData(ArraySegment<byte> data) {
			var length = data.Count;

			yield return new ArraySegment<byte>(
				new[] {(byte)length, (byte)(length >> 8), (byte)(length >> 16), (byte)(length >> 24)});
			yield return data;
		}

		public void RegisterMessageArrivedCallback(Action<ArraySegment<byte>> handler) {
			if (handler == null)
				throw new ArgumentNullException("handler");

			_receivedHandler = handler;
		}
	}
}
