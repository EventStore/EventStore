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
using System.Text;

namespace EventStore.Transport.Http.EntityManagement
{
    public static class HttpEntityManagerExtensions
    {
        public static void ReplyStatus(this HttpEntityManager self, int code, string description, Action<Exception> onError)
        {
            self.Reply(null, code, description, null, null, null, onError);
        }

        public static void ReplyTextContent(this HttpEntityManager self, 
                                            string response, 
                                            int code, 
                                            string description, 
                                            string type,
                                            IEnumerable<KeyValuePair<string, string>> headers, 
                                            Action<Exception> onError)
        {
            //TODO: add encoding header???
            self.Reply(Encoding.UTF8.GetBytes(response ?? string.Empty), code, description, type, Encoding.UTF8, headers, onError);
        }

        public static void ContinueReplyTextContent(
            this HttpEntityManager self, string response, Action<Exception> onError, Action completed)
        {
            //TODO: add encoding header???
            var bytes = Encoding.UTF8.GetBytes(response ?? string.Empty);
            self.ContinueReply(bytes, onError, completed);
        }

        public static void ReadTextRequestAsync(
            this HttpEntityManager self, Action<HttpEntityManager, string> onSuccess, Action<Exception> onError)
        {
            self.ReadRequestAsync(
                (manager, bytes) =>
                {
                    int offset = 0;

                    // check for UTF-8 BOM (0xEF, 0xBB, 0xBF) and skip it safely, if any
                    if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                        offset = 3;

                    onSuccess(manager, Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset));
                }, 
                onError);
        }

    }
}
