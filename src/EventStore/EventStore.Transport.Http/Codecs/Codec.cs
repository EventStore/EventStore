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

using System.Text;
using EventStore.Common.Utils;

namespace EventStore.Transport.Http.Codecs
{
    public static class Codec
    {
        public static readonly NoCodec NoCodec = new NoCodec();
        public static readonly ICodec[] NoCodecs = new ICodec[0];
        public static readonly ManualEncoding ManualEncoding = new ManualEncoding();

        public static readonly JsonCodec Json = new JsonCodec();
        public static readonly XmlCodec Xml = new XmlCodec();
        public static readonly CustomCodec ApplicationXml = new CustomCodec(Xml, ContentType.ApplicationXml, Helper.UTF8NoBom);
        public static readonly CustomCodec EventXml = new CustomCodec(Xml, ContentType.EventXml, Helper.UTF8NoBom);
        public static readonly CustomCodec EventJson = new CustomCodec(Json, ContentType.EventJson, Helper.UTF8NoBom);
        public static readonly CustomCodec EventsXml = new CustomCodec(Xml, ContentType.EventsXml, Helper.UTF8NoBom);
        public static readonly CustomCodec EventsJson = new CustomCodec(Json, ContentType.EventsJson, Helper.UTF8NoBom);
        public static readonly TextCodec Text = new TextCodec();

        public static ICodec CreateCustom(ICodec codec, string contentType, Encoding encoding)
        {
            return new CustomCodec(codec, contentType, encoding);
        }
    }
}