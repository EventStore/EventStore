// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.Collections.Generic;
using System.Threading;
using EventStore.Common.Log;

namespace EventStore.Projections.Core.v8
{
    public class PreludeScript : IDisposable
    {
        private readonly ILogger _systemLogger = LogManager.GetLoggerFor<PreludeScript>();
        private readonly Func<string, Tuple<string, string>> _getModuleSourceAndFileName;
        private readonly Action<string> _logger;
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
            Action<int, Action> cancelCallbackFactory, Action<string> logger = null)
        {
            _logDelegate = LogHandler;
            _loadModuleDelegate = GetModule;
            _getModuleSourceAndFileName = getModuleSourceAndFileName;
            _logger = logger;
            _enterCancellableRegion = EnterCancellableRegion;
            _exitCancellableRegion = ExitCancellableRegion;
            _cancelCallbackFactory = cancelCallbackFactory;
            _script = CompileScript(script, fileName);
        }

        private CompiledScript CompileScript(string script, string fileName)
        {
            try
            {
                ScheduleTerminateExecution();
                IntPtr prelude = Js1.CompilePrelude(
                    script, fileName, _loadModuleDelegate, _enterCancellableRegion, _exitCancellableRegion, _logDelegate);
                CancelTerminateExecution();
                CompiledScript.CheckResult(prelude, false, disposeScriptOnException: true);
                return new CompiledScript(prelude, fileName);
            }
            catch (DllNotFoundException ex)
            {
                throw new ApplicationException(
                    "The projection subsystem failed to load a libjs1.so/js1.dll/... or one of its dependencies.  The original error message is: "
                    + ex.Message, ex);
            }
        }

        private void LogHandler(string message)
        {
            if (_logger != null)
                _logger(message);
            else
                Console.WriteLine(message);
        }

        private IntPtr GetModule(string moduleName)
        {
            try
            {
                var moduleSourceAndFileName = GetModuleSourceAndFileName(moduleName);
                // NOTE: no need to schedule termination; modules are loaded only in context 
                if (_cancelTokenOrStatus == NonScheduled)
                    throw new InvalidOperationException("Requires scheduled terminate execution");
                var compiledModuleHandle = Js1.CompileModule(
                    GetHandle(), moduleSourceAndFileName.Item1, moduleSourceAndFileName.Item2);
                CompiledScript.CheckResult(compiledModuleHandle, terminated: false, disposeScriptOnException: true);
                var compiledModule = new CompiledScript(compiledModuleHandle, moduleSourceAndFileName.Item2);
                _modules.Add(compiledModule);
                return compiledModuleHandle;
            }
            catch (Exception ex)
            {
                _systemLogger.ErrorException(ex, "Cannot load module '{0}'", moduleName);
                //TODO: this is not a good way to report missing module and other exceptions back to caller
                return IntPtr.Zero;
            }
        }

        private Tuple<string, string> GetModuleSourceAndFileName(string moduleName)
        {
            return _getModuleSourceAndFileName(moduleName);
        }

        public void Dispose()
        {
            _modules.ForEach(v => v.Dispose());
            _script.Dispose();
        }

        public IntPtr GetHandle()
        {
            return _script != null ? _script.GetHandle() : IntPtr.Zero;
        }

        private const int NonScheduled = 0;
        private const int Scheduled = -2;
        private const int Terminating = -1;

        private void CancelExecution(IntPtr scriptHandle)
        {
            Js1.TerminateExecution(scriptHandle);
        }

        private void AnotherThreadCancel(int cancelToken, IntPtr scriptHandle, Action expired)
        {
            expired(); // always set our termination timout expired 
            if (Interlocked.CompareExchange(ref _cancelTokenOrStatus, Terminating, cancelToken) == cancelToken)
            {
                if (scriptHandle != IntPtr.Zero) // prelude itself does not yet handle.  // TODO: handle somehow?
                    CancelExecution(scriptHandle);
                if (Interlocked.CompareExchange(ref _cancelTokenOrStatus, Scheduled, Terminating) != Terminating)
                    throw new Exception();
            }
        }

        private class CancelRef
        {
            public bool TerminateRequested = false;

            public void Terminate()
            {
                TerminateRequested = true;
            }
        }

        public void ScheduleTerminateExecution()
        {
            int currentCancelToken = ++_currentCancelToken;
            if (Interlocked.CompareExchange(ref _cancelTokenOrStatus, Scheduled, NonScheduled) != NonScheduled) //TODO: no need for interlocked?
                throw new InvalidOperationException("ScheduleTerminateExecution cannot be called while previous one has not been canceled");
            if (_cancelCallbackFactory != null) // allow nulls in tests
            {
                var terminateRequested = new CancelRef();
                _terminateRequested = terminateRequested;
                _cancelCallbackFactory(
                    1000, () => AnotherThreadCancel(currentCancelToken, GetHandle(), terminateRequested.Terminate));
            }
            else
            {
                _terminateRequested = _defaultCancelRef;
            }
        }

        public bool CancelTerminateExecution()
        {
            //NOTE: cannot be attempted while running, but it can be attempted while terminating
            while (Interlocked.CompareExchange(ref _cancelTokenOrStatus, NonScheduled, Scheduled) != Scheduled)
                Thread.SpinWait(1);
            // exit only if terminated or canceled
            return _terminateRequested.TerminateRequested;
        }

        private bool EnterCancellableRegion()
        {
            var entered = Interlocked.CompareExchange(ref _cancelTokenOrStatus, _currentCancelToken, Scheduled) == Scheduled;
            var result = entered && !_terminateRequested.TerminateRequested;
            if (!result && entered)
            {
                if (Interlocked.CompareExchange(ref _cancelTokenOrStatus, Scheduled, _currentCancelToken) != _currentCancelToken)
                    throw new Exception();
            }
            return result;
        }

        /// <summary>
        /// returns False if terminated (or inappropriate invocation)
        /// </summary>
        /// <returns></returns>
        private bool ExitCancellableRegion()
        {
            return Interlocked.CompareExchange(ref _cancelTokenOrStatus, Scheduled, _currentCancelToken) == _currentCancelToken
                   && !_terminateRequested.TerminateRequested;
        }

    }
}