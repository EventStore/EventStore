using System;
using System.Collections.Generic;
using System.Threading;
using EventStore.Common.Log;

namespace EventStore.Projections.Core.v8 {
	public class PreludeScript : IDisposable {
		private const int CompileTimeoutMs = 5000;
		private const int RetryLimit = 3;

		private readonly ILogger Log = LogManager.GetLoggerFor<PreludeScript>();
		private readonly Func<string, Tuple<string, string>> _getModuleSourceAndFileName;
		private readonly Action<string, object[]> _logger;
		private readonly CompiledScript _script;
		private readonly List<CompiledScript> _modules = new List<CompiledScript>();

		private readonly Js1.LoadModuleDelegate _loadModuleDelegate;
		// do not inline.  this delegate is required to be kept alive to be available to unmanaged code

		private readonly Js1.LogDelegate _logDelegate;
		// a reference must be kept to make a delegate callable from unmanaged world

		private readonly Action<int, Action> _cancelCallbackFactory;

		private int _cancelTokenOrStatus = 0; // bot hcore and timer threads
		private readonly CancelRef _defaultCancelRef = new CancelRef();
		private CancelRef _terminateRequested; // core thread only
		private int _currentCancelToken = 1; // core thread only

		private readonly Js1.EnterCancellableRegionDelegate _enterCancellableRegion;
		private readonly Js1.ExitCancellableRegionDelegate _exitCancellableRegion;

		public PreludeScript(
			string script, string fileName, Func<string, Tuple<string, string>> getModuleSourceAndFileName,
			Action<int, Action> cancelCallbackFactory, Action<string, object[]> logger = null) {
			_logDelegate = LogHandler;
			_loadModuleDelegate = GetModule;
			_getModuleSourceAndFileName = getModuleSourceAndFileName;
			_logger = logger;
			_enterCancellableRegion = EnterCancellableRegion;
			_exitCancellableRegion = ExitCancellableRegion;
			_cancelCallbackFactory = cancelCallbackFactory;
			_script = CompileScript(script, fileName);
		}

		private CompiledScript CompileScript(string script, string fileName) {
			try {
				var attempts = RetryLimit;
				var prelude = default(IntPtr);
				do {
					attempts--;
					try {
						ScheduleTerminateExecution();
						prelude = Js1.CompilePrelude(
							script, fileName, _loadModuleDelegate, _enterCancellableRegion, _exitCancellableRegion,
							_logDelegate);
						CancelTerminateExecution();
						CompiledScript.CheckResult(prelude, false, disposeScriptOnException: true);
					} catch (Js1Exception ex) {
						if (attempts > 0 && (ex.ErrorCode == -1 || ex.ErrorCode == -2)) {
							// timeouts
							Thread.Sleep(2000);
						} else throw;
					}
				} while (prelude == default(IntPtr));

				return new CompiledScript(prelude);
			} catch (DllNotFoundException ex) {
				Log.Info("{ex}\n{e}\n{stackTrace}", ex.ToString(), ex.Message, ex.StackTrace);
				throw new ApplicationException(
					"The projection subsystem failed to load a libjs1.so/js1.dll/... or one of its dependencies.  The original error message is: "
					+ ex.Message, ex);
			}
		}

		private void LogHandler(string message) {
			if (_logger != null)
				_logger(message, new object[] { });
			else
				Log.Debug(message);
		}

		private IntPtr GetModule(IntPtr prelude, string moduleName) {
			try {
				var moduleSourceAndFileName = GetModuleSourceAndFileName(moduleName);
				// NOTE: no need to schedule termination; modules are loaded only in context 
				if (_cancelTokenOrStatus == NonScheduled)
					throw new InvalidOperationException("Requires scheduled terminate execution");
				var compiledModuleHandle = Js1.CompileModule(
					prelude, moduleSourceAndFileName.Item1, moduleSourceAndFileName.Item2);
				CompiledScript.CheckResult(compiledModuleHandle, terminated: false, disposeScriptOnException: true);
				var compiledModule = new CompiledScript(compiledModuleHandle);
				_modules.Add(compiledModule);
				return compiledModuleHandle;
			} catch (Exception ex) {
				Log.ErrorException(ex, "Cannot load module '{module}'", moduleName);
				//TODO: this is not a good way to report missing module and other exceptions back to caller
				return IntPtr.Zero;
			}
		}

		private Tuple<string, string> GetModuleSourceAndFileName(string moduleName) {
			return _getModuleSourceAndFileName(moduleName);
		}

		public void Dispose() {
			_modules.ForEach(v => v.Dispose());
			_script.Dispose();
		}

		public IntPtr GetHandle() {
			return _script != null ? _script.GetHandle() : IntPtr.Zero;
		}

		private const int NonScheduled = 0;
		private const int Scheduled = -2;
		private const int Terminating = -1;

		private void CancelExecution(IntPtr scriptHandle) {
			Js1.TerminateExecution(scriptHandle);
		}

		private void AnotherThreadCancel(int cancelToken, IntPtr scriptHandle, Action expired) {
			expired(); // always set our termination timout expired 
			if (Interlocked.CompareExchange(ref _cancelTokenOrStatus, Terminating, cancelToken) == cancelToken) {
				if (scriptHandle != IntPtr.Zero) // prelude itself does not yet handle.  // TODO: handle somehow?
					CancelExecution(scriptHandle);
				if (Interlocked.CompareExchange(ref _cancelTokenOrStatus, Scheduled, Terminating) != Terminating)
					throw new Exception();
			}
		}

		private class CancelRef {
			public bool TerminateRequested = false;

			public void Terminate() {
				TerminateRequested = true;
			}
		}

		public void ScheduleTerminateExecution() {
			int currentCancelToken = ++_currentCancelToken;
			if (Interlocked.CompareExchange(ref _cancelTokenOrStatus, Scheduled, NonScheduled) != NonScheduled
			) //TODO: no need for interlocked?
				throw new InvalidOperationException(
					"ScheduleTerminateExecution cannot be called while previous one has not been canceled");
			if (_cancelCallbackFactory != null) // allow nulls in tests
			{
				var terminateRequested = new CancelRef();
				_terminateRequested = terminateRequested;
				_cancelCallbackFactory(
					CompileTimeoutMs,
					() => AnotherThreadCancel(currentCancelToken, GetHandle(), terminateRequested.Terminate));
			} else {
				_terminateRequested = _defaultCancelRef;
			}
		}

		public bool CancelTerminateExecution() {
			//NOTE: cannot be attempted while running, but it can be attempted while terminating
			while (Interlocked.CompareExchange(ref _cancelTokenOrStatus, NonScheduled, Scheduled) != Scheduled)
				Thread.SpinWait(1);
			// exit only if terminated or canceled
			return _terminateRequested.TerminateRequested;
		}

		private bool EnterCancellableRegion() {
			var entered = Interlocked.CompareExchange(ref _cancelTokenOrStatus, _currentCancelToken, Scheduled) ==
			              Scheduled;
			var result = entered && !_terminateRequested.TerminateRequested;
			if (!result && entered) {
				if (Interlocked.CompareExchange(ref _cancelTokenOrStatus, Scheduled, _currentCancelToken) !=
				    _currentCancelToken)
					throw new Exception();
			}

			return result;
		}

		/// <summary>
		/// returns False if terminated (or inappropriate invocation)
		/// </summary>
		/// <returns></returns>
		private bool ExitCancellableRegion() {
			return Interlocked.CompareExchange(ref _cancelTokenOrStatus, Scheduled, _currentCancelToken) ==
			       _currentCancelToken
			       && !_terminateRequested.TerminateRequested;
		}
	}
}
