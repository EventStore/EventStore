using System;
using System.IO;
using EventStore.BufferManagement;

namespace EventStore.Transport.Tcp.Formatting {
	/// <summary>
	/// Formats a message for transport using ProtoBuf serialization
	/// </summary>
	public class ProtoBufMessageFormatter<T> : FormatterBase<T> {
		private readonly BufferManager _bufferManager;
		private readonly int _initialBuffers;

		/// <summary>
		/// Initializes a new instance of the <see cref="ProtoBufMessageFormatter{T}"/> class.
		/// </summary>
		public ProtoBufMessageFormatter() : this(BufferManager.Default, 2) {
		}


		/// <summary>
		/// Initializes a new instance of the <see cref="ProtoBufMessageFormatter{T}"/> class.
		/// </summary>
		/// <param name="bufferManager">The buffer manager.</param>
		public ProtoBufMessageFormatter(BufferManager bufferManager) : this(bufferManager, 2) {
		}


		/// <summary>
		/// Initializes a new instance of the <see cref="ProtoBufMessageFormatter{T}"/> class.
		/// </summary>
		/// <param name="bufferManager">The buffer manager.</param>
		/// <param name="initialBuffers">The number of initial buffers.</param>
		public ProtoBufMessageFormatter(BufferManager bufferManager, int initialBuffers) {
			_bufferManager = bufferManager;
			_initialBuffers = initialBuffers;
		}

		/// <summary>
		/// Gets a <see cref="BufferPool"></see> representing the IMessage provided.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <returns>A <see cref="BufferPool"></see> with a representation of the message</returns>
		public override BufferPool ToBufferPool(T message) {
			if (message == null)
				throw new ArgumentNullException("message");

			var bufferPool = new BufferPool(_initialBuffers, _bufferManager);
			var stream = new BufferPoolStream(bufferPool);
			ProtoBuf.Serializer.Serialize(stream, message);
			return bufferPool;
		}

		/// <summary>
		/// Creates a message object from the specified stream
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <returns>A message object</returns>
		public override T From(Stream stream) {
			if (stream == null)
				throw new ArgumentNullException("stream");
			return ProtoBuf.Serializer.Deserialize<T>(stream);
		}
	}
}
