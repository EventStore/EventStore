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
using System.Linq;

namespace EventStore.Transport.Http
{
    public class AcceptComponent
    {
        public readonly string MediaRange;
        public readonly string MediaType;
        public readonly string MediaSubtype;
        public readonly float Priority;

        public readonly string[] Parameters;

        public AcceptComponent(string mediaRange, string mediaType, string mediaSubtype, float priority, string[] parameters)
        {
            MediaRange = mediaRange;
            MediaType = mediaType;
            MediaSubtype = mediaSubtype;
            Priority = priority;

            Parameters = parameters ?? Common.Utils.Empty.StringArray;
        }

        public static bool TryParse(string componentText, out AcceptComponent result)
        {
            return TryParseInternal(componentText, false, out result);
        }

        public static AcceptComponent TryParse(string componentText)
        {
            AcceptComponent result;
            return TryParseInternal(componentText, false, out result) ? result : null;
        }

        public static AcceptComponent Parse(string componentText)
        {
            AcceptComponent result;
            if (!TryParseInternal(componentText, true, out result))
                throw new Exception("This should never happen!");
            return result;
        }

        private static bool TryParseInternal(string componentText, bool throwExceptions, out AcceptComponent result)
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

            float priority = 1.0f; // default priority

            string[] parts = componentText.Split(';');
            string mediaRange = parts[0];

            string[] typeParts = mediaRange.Split(new[] { '/' }, 2);
            if (typeParts.Length != 2)
            {
                if (throwExceptions)
                    throw new ArgumentException("componentText");
                return false;
            }

            string mediaType = typeParts[0];
            string mediaSubtype = typeParts[1];
            string[] parameters = null;

            if (parts.Length > 1)
            {
                var firstPart = parts[1];
                var additionSkip = 0;
                if (firstPart.StartsWith("q="))
                {
                    var quality = firstPart.Substring(2);
                    float q;
                    if (float.TryParse(quality, NumberStyles.Float, CultureInfo.InvariantCulture, out q))
                    {
                        priority = q;
                        additionSkip = 1;
                    }
                }
                parameters = parts.Skip(1 + additionSkip).ToArray();
            }

            result = new AcceptComponent(mediaRange, mediaType, mediaSubtype, priority, parameters);
            return true;
        }
    }
}
