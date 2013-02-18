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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Transport.Http;

namespace EventStore.Core.Services.Monitoring.Utils
{
    public class ShellExecutor
    {
        private Process _process;
        private readonly MemoryStream _outputStream = new MemoryStream();

        private Action<Exception, string> _callback;
        public readonly string Command;
        public readonly string Args;

        public ShellExecutor(string command, string args = null)
        {
            Ensure.NotNull(command, "command");

            Command = command;
            Args = args;
        }

        public static string GetOutput(string command, string args = null)
        {
            var info = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                FileName = command,
                Arguments = args ?? string.Empty
            };

            using (var process = Process.Start(info))
            {
                var res = process.StandardOutput.ReadToEnd();
                return res;
            }
        }

        public void GetOutputAsync(Action<Exception, string> callback)
        {
            Ensure.NotNull(callback, "callback");
            _callback = callback;

            var info = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                FileName = Command,
                Arguments = Args ?? string.Empty
            };

            // note MM: takes time to start a process. no async API available
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Stream inputStream;
                try
                {
                    _process = Process.Start(info);
                    inputStream = _process.StandardOutput.BaseStream;
                }
                catch (Exception ex)
                {
                    OnCompleted(ex, null);
                    return;
                }

                var copier = new AsyncStreamCopier<object>(inputStream, _outputStream, null, OnStreamCopied);
                copier.Start();
            });
        }

        private void OnStreamCopied(AsyncStreamCopier<object> copier)
        {
            if (copier.Error != null)
            {
                OnCompleted(copier.Error, null);
            }
            else
            {
                var output = Encoding.UTF8.GetString(_outputStream.GetBuffer(), 0, (int)_outputStream.Length);
                OnCompleted(null, output);
            }
        }

        private void OnCompleted(Exception ex, string output)
        {
            CleanUp();
            _callback(ex, output);
        }

        private void CleanUp()
        {
            if (_outputStream != null)
                _outputStream.Dispose();
            if (_process != null)
                _process.Dispose();
        }
    }
}
