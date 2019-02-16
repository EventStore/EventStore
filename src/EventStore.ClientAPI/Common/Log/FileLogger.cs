using System;
using System.IO;

namespace EventStore.ClientAPI.Common.Log {
	/// <summary>
	/// Logger that writes to a file
	/// </summary>
	class FileLogger : ILogger, IDisposable {
		private StreamWriter _streamWriter;

		public FileLogger(string filename) {
			OpenFile(filename);
		}

		private void OpenFile(string filename) {
			_streamWriter = new StreamWriter(filename) {AutoFlush = true};
		}

		public void Error(string format, params object[] args) {
			_streamWriter.WriteLine("Error: " + format, args);
		}

		public void Error(Exception ex, string format, params object[] args) {
			_streamWriter.WriteLine("Error: " + format, args);
			_streamWriter.WriteLine(ex);
		}

		public void Info(string format, params object[] args) {
			_streamWriter.WriteLine("Info : " + format, args);
		}

		public void Info(Exception ex, string format, params object[] args) {
			_streamWriter.WriteLine("Info : " + format, args);
			_streamWriter.WriteLine(ex);
		}

		public void Debug(string format, params object[] args) {
			_streamWriter.WriteLine("Debug: " + format, args);
		}

		public void Debug(Exception ex, string format, params object[] args) {
			_streamWriter.WriteLine("Debug: " + format, args);
			_streamWriter.WriteLine(ex);
		}

		void IDisposable.Dispose() {
			if (_streamWriter == null) return;
			_streamWriter.Close();
			_streamWriter.Dispose();
		}
	}
}
