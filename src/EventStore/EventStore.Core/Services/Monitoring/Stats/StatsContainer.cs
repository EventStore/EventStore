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
using System.Linq;
using EventStore.Common.Utils;

namespace EventStore.Core.Services.Monitoring.Stats
{
    public class StatsContainer
    {
        private readonly Dictionary<string, object> _stats = new Dictionary<string, object>();

        private const string Separator = "-";
        private static readonly string[] SplitSeparator = new[] {Separator};

        public void Add(IDictionary<string, object> statGroup)
        {
            Ensure.NotNull(statGroup, "statGroup");

            foreach (var stat in statGroup)
                _stats.Add(stat.Key, stat.Value);
        }

        public Dictionary<string, object> GetRawStats()
        {
            return new Dictionary<string, object>(_stats);
        }

        public Dictionary<string, object> GetGroupedStats()
        {
            var grouped = Group(_stats);
            return grouped;
        }

        private static Dictionary<string, object> Group(Dictionary<string, object> input)
        {
            Ensure.NotNull(input, "input");

            if (input.IsEmpty())
                return input;

            var groupContainer = NewDictionary();
            var hasSubGroups = false;

            foreach (var entry in input)
            {
                var groups = entry.Key.Split(SplitSeparator, StringSplitOptions.RemoveEmptyEntries);
                if (groups.Length < 2)
                {
                    groupContainer.Add(entry.Key, entry.Value);
                    continue;
                }

                hasSubGroups = true;

                string prefix = groups[0];
                string remaining = string.Join(Separator, groups.Skip(1).ToArray());

                if (!groupContainer.ContainsKey(prefix))
                    groupContainer.Add(prefix, NewDictionary());

                ((Dictionary<string, object>)groupContainer[prefix]).Add(remaining, entry.Value);
            }

            if (!hasSubGroups)
                return groupContainer;

            // we must first iterate through all dictionary and then aggregate it again
            var result = NewDictionary();

            foreach (var entry in groupContainer)
            {
                var subgroup = entry.Value as Dictionary<string, object>;
                if (subgroup != null)
                    result[entry.Key] = Group(subgroup);
                else
                    result[entry.Key] = entry.Value;
            }

            return result;
        }

        private static Dictionary<string,object> NewDictionary()
        {
            return new Dictionary<string, object>(new CaseInsensitiveStringComparer());
        }

        private class CaseInsensitiveStringComparer: IEqualityComparer<string>
        {
            public bool Equals(string x, string y)
            {
                return string.Equals(x, y, StringComparison.InvariantCultureIgnoreCase);
            }

            public int GetHashCode(string obj)
            {
                return obj != null ? obj.ToUpperInvariant().GetHashCode() : -1;
            }
        }
    }
}
