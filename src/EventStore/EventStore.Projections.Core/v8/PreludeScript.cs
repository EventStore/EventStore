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
using js1test;

namespace EventStore.Projections.Core.v8
{
    public class PreludeScript : IDisposable
    {
        private readonly Func<string, Tuple<string, string>> _getModuleSourceAndFileName;
        private readonly Action<string> _logger;
        private readonly CompiledScript _script;
        private readonly List<CompiledScript> _modules = new List<CompiledScript>();

        private readonly Js1.LoadModuleDelegate _loadModuleDelegate;
                                                // do not inline.  this delegate is required to be kept alive to be available to unmanaged code

        private readonly Js1.LogDelegate _logDelegate;
                                         // a reference must be kept to make a delegate callable from unmanaged world

        public PreludeScript(
            string script, string fileName, Func<string, Tuple<string, string>> getModuleSourceAndFileName,
            Action<string> logger = null)
        {
            _logDelegate = LogHandler;
            _loadModuleDelegate = GetModule;
            _getModuleSourceAndFileName = getModuleSourceAndFileName;
            _logger = logger;
            _script = CompileScript(script, fileName);
        }

        private CompiledScript CompileScript(string script, string fileName)
        {
            IntPtr prelude = Js1.CompilePrelude(script, fileName, _loadModuleDelegate, _logDelegate);
            CompiledScript.CheckResult(prelude, disposeScriptOnException: true);
            return new CompiledScript(prelude, fileName);
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
                var compiledModuleHandle = Js1.CompileModule(
                    GetHandle(), moduleSourceAndFileName.Item1, moduleSourceAndFileName.Item2);
                CompiledScript.CheckResult(compiledModuleHandle, disposeScriptOnException: true);
                var compiledModule = new CompiledScript(compiledModuleHandle, moduleSourceAndFileName.Item2);
                _modules.Add(compiledModule);
                return compiledModuleHandle;
            }
            catch (Exception)
            {
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
    }
}