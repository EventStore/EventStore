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
using System.IO;
using System.Net;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.Transport.Http
{
    internal class ClientOperationState: IDisposable
    {
        public readonly HttpWebRequest Request;
        public readonly Action<HttpResponse> OnSuccess;
        public readonly Action<Exception> OnError;

        public HttpResponse Response { get; set; }

        public Stream InputStream { get; set; }
        public Stream OutputStream { get; set; }

        private readonly ILogger _log;

        public ClientOperationState(ILogger log, HttpWebRequest request, Action<HttpResponse> onSuccess, Action<Exception> onError)
        {
            Ensure.NotNull(log, "log");
            Ensure.NotNull(request, "request");
            Ensure.NotNull(onSuccess, "onSuccess");
            Ensure.NotNull(onError, "onError");

            _log = log;
            
            Request = request;
            OnSuccess = onSuccess;
            OnError = onError;
        }

        public void Dispose()
        {
            SafelyDispose(InputStream, OutputStream);
        }

        private void SafelyDispose(params Stream[] streams)
        {
            if (streams == null || streams.Length == 0)
                return;

            foreach (var stream in streams)
            {
                try
                {
                    if (stream != null)
                        stream.Dispose();
                }
                catch (Exception e)
                {
                    //Exceptions may be thrown when client shutdown and we were unable to write all the data,
                    //Nothing we can do, ignore (another option - globally ignore write errors)
                    _log.Debug("Error while closing stream : {0}", e.Message);
                }
            }
        }
    }
}