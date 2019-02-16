using System;

namespace EventStore.Projections.Core.v8 {
	class CompiledScript : IDisposable {
		private IntPtr _script;
		private bool _disposed = false;
		private static Js1.ReportErrorDelegate _reportErrorCallback;

		public CompiledScript(IntPtr script) {
			_script = script;
		}

		public void Dispose() {
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}


		public static void CheckResult(IntPtr scriptHandle, bool terminated, bool disposeScriptOnException) {
			if (terminated)
				throw new Js1Exception(
					-2, "Failed to compile script. Script execution terminated.  Timeout expired. (1)");
			if (scriptHandle == IntPtr.Zero)
				throw new Js1Exception(
					-1, "Failed to compile script. Script execution terminated.  Timeout expired. (2)");
			int? errorCode = null;
			string errorMessage = null;
			_reportErrorCallback =
				// NOTE: this local delegate must go to a field to keep references while ReportErrors is being executed
				(code, message) => {
					//NOTE: do not throw exceptions directly in this handler
					// let the CPP code clean up 
					errorCode = code;
					errorMessage = message;
				};
			Js1.ReportErrors(scriptHandle, _reportErrorCallback);
			if (errorCode != null) {
				if (disposeScriptOnException) {
					Js1.DisposeScript(scriptHandle);
				}

				if (errorCode == 2)
					throw new Js1Exception(
						errorCode.Value,
						"Failed to compile script. Script execution terminated.  Timeout expired. (3)");
				throw new Js1Exception(errorCode.Value, errorMessage);
			}
		}

		private void Dispose(bool disposing) {
			if (_disposed)
				return;
			var scriptHandle = _script;
			_script = IntPtr.Zero;
			Js1.DisposeScript(scriptHandle);
			_disposed = true;
		}

		~CompiledScript() {
			Dispose(disposing: false);
		}

		internal IntPtr GetHandle() {
			return _script;
		}
	}
}
