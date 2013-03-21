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
using System.Globalization;
using System.Text;

namespace EventStore.Transport.Http
{
    public class MediaType
    {
        public readonly string Range;
        public readonly string Type;
        public readonly string Subtype;
        public readonly float Priority;
        public readonly bool EncodingSpecified;
        public readonly Encoding Encoding;

        public MediaType(string range, string type, string subtype, float priority, bool encodingSpecified, Encoding encoding)
        {
            Range = range;
            Type = type;
            Subtype = subtype;
            Priority = priority;
            EncodingSpecified = encodingSpecified;
            Encoding = encoding;
        }

        public MediaType(string range, string type, string subtype, float priority) 
            : this(range, type, subtype, priority, false, null)
        {
        }

        public bool Matches(string mediaRange, Encoding encoding)
        {
            if (EncodingSpecified)
                return Range == mediaRange && Encoding.Equals(encoding);
            
            return Range == mediaRange;
        }

        public static bool TryParse(string componentText, out MediaType result)
        {
            return TryParseInternal(componentText, false, out result);
        }

        public static MediaType TryParse(string componentText)
        {
            MediaType result;
            return TryParseInternal(componentText, false, out result) ? result : null;
        }

        public static MediaType Parse(string componentText)
        {
            MediaType result;
            if (!TryParseInternal(componentText, true, out result))
                throw new Exception("This should never happen!");
            return result;
        }

        private static bool TryParseInternal(string componentText, bool throwExceptions, out MediaType result)
        {
            result = null;

            if (componentText == null)
            {
                if (throwExceptions)
                    throw new ArgumentNullException("componentText");
                return false;
            }
            if (componentText == "")
            {
                if (throwExceptions)
                    throw new ArgumentException("componentText");
                return false;
            }

            var priority = 1.0f; // default priority
            var encodingSpecified = false;
            Encoding encoding = null;

            var parts = componentText.Split(';');
            var mediaRange = parts[0];

            var typeParts = mediaRange.Split(new[] { '/' }, 2);
            if (typeParts.Length != 2)
            {
                if (throwExceptions)
                    throw new ArgumentException("componentText");
                return false;
            }

            var mediaType = typeParts[0];
            var mediaSubtype = typeParts[1];

            if (parts.Length > 1)
            {
                for (var i = 1; i < parts.Length; i++)
                {
                    var part = parts[i].ToLowerInvariant().Trim();

                    if (part.StartsWith("q="))
                    {
                        var quality = part.Substring(2);
                        float q;
                        if (float.TryParse(quality, NumberStyles.Float, CultureInfo.InvariantCulture, out q))
                        {
                            priority = q;
                        }
                    } else if (part.StartsWith("charset="))
                    {
                        encodingSpecified = true;
                        try
                        {
                            encoding = Encoding.GetEncoding(part.Substring(8));
                        } catch (ArgumentException) {
                            encoding = null;
                        }
                    }
                }
            }

            result = new MediaType(mediaRange, mediaType, mediaSubtype, priority, encodingSpecified, encoding);
            return true;
        }
    }
}
