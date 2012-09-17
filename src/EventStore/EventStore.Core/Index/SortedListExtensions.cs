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
using System.Collections.Generic;

namespace EventStore.Core.Index
{
    public static class SortedListExtensions
    {
        /// <summary>
        /// Returns the index of smallest (according to comparer) element greater than or equal to provided key.
        /// Returns -1 if all keys are smaller than provided key.
        /// </summary>
        public static int LowerBound<TKey, TValue>(this SortedList<TKey, TValue> list, TKey key)
        {
            if (list.Count == 0)
                return -1;

            var comparer = list.Comparer;
            if (comparer.Compare(list.Keys[list.Keys.Count - 1], key) < 0)
                return -1; // if all elements are smaller, then no lower bound

            int l = 0;
            int r = list.Count - 1;
            while (l < r)
            {
                int m = l + (r - l) / 2;
                if (comparer.Compare(list.Keys[m], key) >= 0)
                    r = m;
                else
                    l = m + 1;
            }

            return r;
        }

        /// <summary>
        /// Returns the index of largest (according to comparer) element less than or equal to provided key.
        /// Returns -1 if all keys are greater than provided key.
        /// </summary>
        public static int UpperBound<TKey, TValue>(this SortedList<TKey, TValue> list, TKey key)
        {
            if (list.Count == 0)
                return -1;

            var comparer = list.Comparer;
            if (comparer.Compare(key, list.Keys[0]) < 0)
                return -1; // if all elements are greater, then no upper bound

            int l = 0;
            int r = list.Count - 1;
            while (l < r)
            {
                int m = l + (r - l + 1) / 2;
                if (comparer.Compare(list.Keys[m], key) <= 0)
                    l = m;
                else
                    r = m - 1;
            }

            return l;
        }
    }
}