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
using System.Net;
using EventStore.Common.Utils;
using Uri = System.Uri;

namespace EventStore.Transport.Http.EntityManagement
{
    public class HttpEntityReceivedComparer : IComparer<HttpEntity>
    {
        public int Compare(HttpEntity x, HttpEntity y)
        {
            return x.Received.CompareTo(y.Received);
        }
    }

    public class HttpEntity
    {
        public readonly HttpEntityManager Manager;

        public readonly DateTime Received;
        public readonly string UserHostName;

        public readonly ICodec RequestCodec;
        public readonly ICodec ResponseCodec;

        internal HttpListenerContext Context;
        internal HttpListenerRequest Request;
        internal HttpListenerResponse Response;

        public Uri RequestBaseUri 
        {
            get { return Request.Url; }
        }

        public HttpEntity(DateTime received,
                          string userHostName,
                          ICodec requestCodec,
                          ICodec responseCodec,
                          HttpListenerContext context,
                          HttpListenerRequest request,
                          HttpListenerResponse response,
                          string[] allowedMethods,
                          Action<HttpEntity> onRequestSatisfied)
        {
            Ensure.NotNull(userHostName, "userHostName");
            Ensure.NotNull(requestCodec, "requestCodec");
            Ensure.NotNull(responseCodec, "responseCodec");
            Ensure.NotNull(context, "context");
            Ensure.NotNull(request, "request");
            Ensure.NotNull(response, "response");
            Ensure.NotNull(allowedMethods, "allowedMethods");
            Ensure.NotNull(onRequestSatisfied, "onRequestSatisfied");

            Received = received;
            UserHostName = userHostName;

            RequestCodec = requestCodec;
            ResponseCodec = responseCodec;

            Context = context;
            Request = request;
            Response = response;

            Manager = HttpEntityManager.Create(this, allowedMethods, onRequestSatisfied);
        }
    }
}