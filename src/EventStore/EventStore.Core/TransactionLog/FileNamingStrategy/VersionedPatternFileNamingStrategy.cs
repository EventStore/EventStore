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
using System.Text.RegularExpressions;
using EventStore.Common.Utils;
using System.Linq;

namespace EventStore.Core.TransactionLog.FileNamingStrategy
{
    public class VersionedPatternFileNamingStrategy : IFileNamingStrategy
    {
        private readonly string _path;
        private readonly string _prefix;
        private readonly Regex _chunkNamePattern;

        public VersionedPatternFileNamingStrategy(string path, string prefix)
        {
            Ensure.NotNull(path, "path");
            Ensure.NotNull(prefix, "prefix");
            _path = path;
            _prefix = prefix;

            _chunkNamePattern = new Regex("^" + _prefix + @"\d{6}\.\w{6}$");
        }

        public string GetFilenameFor(int index, int version)
        {
            Ensure.Nonnegative(index, "index");
            Ensure.Nonnegative(version, "version");

            return Path.Combine(_path, string.Format("{0}{1:000000}.{2:000000}", _prefix, index, version));
        }

        public string DetermineBestVersionFilenameFor(int index)
        {
            var allVersions = GetAllVersionsFor(index);
            if (allVersions.Length == 0)
                return GetFilenameFor(index, 0);
            int lastVersion;
            if (!int.TryParse(allVersions[0].Substring(allVersions[0].LastIndexOf('.') + 1), out lastVersion))
                throw new Exception(string.Format("Couldn't determine version from filename '{0}'.", allVersions[0]));
            return GetFilenameFor(index, lastVersion + 1);
        }

        public string[] GetAllVersionsFor(int index)
        {
            var versions = Directory.EnumerateFiles(_path, string.Format("{0}{1:000000}.*", _prefix, index))
                                    .Where(x => _chunkNamePattern.IsMatch(Path.GetFileName(x)))
                                    .OrderByDescending(x => x, StringComparer.CurrentCultureIgnoreCase)
                                    .ToArray();
            return versions;
        }

        public string[] GetAllPresentFiles()
        {
            var versions = Directory.EnumerateFiles(_path, string.Format("{0}*.*", _prefix))
                                    .Where(x => _chunkNamePattern.IsMatch(Path.GetFileName(x)))
                                    .ToArray();
            return versions;
        }

        public string GetTempFilename()
        {
            return Path.Combine(_path, string.Format("{0}.tmp", Guid.NewGuid()));
        }

        public string[] GetAllTempFiles()
        {
            return Directory.GetFiles(_path, "*.tmp");
        }
    }
}