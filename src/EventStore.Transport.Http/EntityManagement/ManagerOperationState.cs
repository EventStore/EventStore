using System;
using System.IO;
using EventStore.Common.Utils;

namespace EventStore.Transport.Http.EntityManagement {
	internal class ManagerOperationState : IDisposable {
		public readonly Action<HttpEntityManager, byte[]> OnReadSuccess;
		public readonly Action<Exception> OnError;

		public readonly Stream InputStream;
		public readonly Stream OutputStream;

		public ManagerOperationState(Stream inputStream,
			Stream outputStream,
			Action<HttpEntityManager, byte[]> onReadSuccess,
			Action<Exception> onError) {
			Ensure.NotNull(inputStream, "inputStream");
			Ensure.NotNull(outputStream, "outputStream");
			Ensure.NotNull(onReadSuccess, "onReadSuccess");
			Ensure.NotNull(onError, "onError");

			InputStream = inputStream;
			OutputStream = outputStream;
			OnReadSuccess = onReadSuccess;
			OnError = onError;
		}

		public void Dispose() {
			IOStreams.SafelyDispose(InputStream, OutputStream);
		}
	}
}
