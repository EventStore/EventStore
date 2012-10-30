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

namespace EventStore.Projections.Core.v8
{
    class CompiledScript : IDisposable
    {
        private IntPtr _script;
        private readonly string _fileName;
        private bool _disposed = false;
        private static Js1.ReportErrorDelegate _reportErrorCallback;

        public CompiledScript(IntPtr script, string fileName)
        {
            _script = script;
            _fileName = fileName;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }


        public static void CheckResult(IntPtr scriptHandle, bool disposeScriptOnException)
        {
            int? errorCode = null;
            string errorMessage = null;
            _reportErrorCallback =
                // NOTE: this local delegate must go to a field to keep references while ReportErrors is being executed
                (code, message) =>
                    {
                        //NOTE: do not throw exceptions directly in this handler
                        // let the CPP code clean up 
                        errorCode = code;
                        errorMessage = message;
                    };
            Js1.ReportErrors(scriptHandle, _reportErrorCallback);
            if (errorCode != null)
            {
                if (disposeScriptOnException)
                {
                    Js1.DisposeScript(scriptHandle);
                }
                throw new Js1Exception(errorCode.Value, errorMessage);
            }
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            var scriptHandle = _script;
            _script = IntPtr.Zero;
            Js1.DisposeScript(scriptHandle);
            _disposed = true;
        }

        ~CompiledScript()
        {
            Dispose(disposing: false);
        }

        internal IntPtr GetHandle()
        {
            return _script;
        }
    }
}
