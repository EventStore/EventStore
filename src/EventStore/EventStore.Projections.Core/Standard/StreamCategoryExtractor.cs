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

namespace EventStore.Projections.Core.Standard
{
    public abstract class StreamCategoryExtractor
    {
        private const string ConfigurationFormatIs = "Configuration format is: \r\nfirst|last\r\nseparator";

        public abstract string GetCategoryByStreamId(string streamId);

        public static StreamCategoryExtractor GetExtractor(string source, Action<string> logger)
        {
            var trimmedSource = source == null ? null : source.Trim();
            if (string.IsNullOrEmpty(source))
                throw new InvalidOperationException(
                    "Cannot initialize categorization projection handler.  "
                    + "One symbol separator or configuration must be supplied in the source.  "
                    + ConfigurationFormatIs);

            if (trimmedSource.Length == 1)
            {
                var separator = trimmedSource[0];
                if (logger != null)
                {
                    logger(
                        String.Format(
                            "Categorize stream projection handler has been initialized with separator: '{0}'", separator));
                }
                var extractor = new StreamCategoryExtractorByLastSeparator(separator);
                return extractor;
            }

            var parts = trimmedSource.Split(new[] { '\n' });

            if (parts.Length != 2)
                throw new InvalidOperationException(
                    "Cannot initialize categorization projection handler.  "
                    + "Invalid configuration  "
                    + ConfigurationFormatIs);

            var direction = parts[0].ToLowerInvariant().Trim();
            if (direction != "first" && direction != "last")
                throw new InvalidOperationException(
                    "Cannot initialize categorization projection handler.  "
                    + "Invalid direction specifier.  Expected 'first' or 'last'. "
                    + ConfigurationFormatIs);

            var separatorLine = parts[1];
            if (separatorLine.Length != 1)
                throw new InvalidOperationException(
                    "Cannot initialize categorization projection handler.  "
                    + "Single separator expected. "
                    + ConfigurationFormatIs);

            switch (direction)
            {
                case "first":
                    return new StreamCategoryExtractorByFirstSeparator(separatorLine[0]);
                case "last":
                    return new StreamCategoryExtractorByLastSeparator(separatorLine[0]);
                default:
                    throw new Exception();

            }
        }
    }
}
