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

using System.Net;

namespace EventStore.ClientAPI.Transport.Http
{
    internal class HttpResponse
    {
        public readonly string CharacterSet;

        public readonly string ContentEncoding;
        public readonly long ContentLength;
        public readonly string ContentType;

        //public readonly CookieCollection Cookies;
        public readonly WebHeaderCollection Headers;

        //public readonly bool IsFromCache;
        //public readonly bool IsMutuallyAuthenticated;//TODO TR: not implemented in mono
        //public readonly DateTime LastModified;

        public readonly string Method;
        //public readonly Version ProtocolVersion;

        //public readonly Uri ResponseUri;
        //public readonly string Server;

        public readonly int HttpStatusCode;
        public readonly string StatusDescription;

        public string Body { get; internal set; }

        public HttpResponse(HttpWebResponse httpWebResponse)
        {
            CharacterSet = httpWebResponse.CharacterSet;

            ContentEncoding = httpWebResponse.ContentEncoding;
            ContentLength = httpWebResponse.ContentLength;
            ContentType = httpWebResponse.ContentType;

            //Cookies = httpWebResponse.Cookies;
            Headers = httpWebResponse.Headers;

            //IsFromCache = httpWebResponse.IsFromCache;
            //IsMutuallyAuthenticated = httpWebResponse.IsMutuallyAuthenticated;

            //LastModified = httpWebResponse.LastModified;

            Method = httpWebResponse.Method;
            //ProtocolVersion = httpWebResponse.ProtocolVersion;

            //ResponseUri = httpWebResponse.ResponseUri;
            //Server = httpWebResponse.Server;

            HttpStatusCode = (int)httpWebResponse.StatusCode;
            StatusDescription = httpWebResponse.StatusDescription;
        }
    }
}