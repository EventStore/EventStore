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

namespace EventStore.Transport.Tcp
{
    internal static class Helper
    {
        /// <summary>
        /// Lazy evaluates a lazy <see cref="IEnumerable{T}"></see> and calls callback when the lazy evaluation is completed
        /// </summary>
        /// <param name="wrapped">The wrapped lazy evaluation</param>
        /// <param name="callback">The callback to call for each passed object after lazy evaluation</param>
        /// <param name="objects">The array of objects to call callback for</param>
        /// <returns>A lazily evaluated IEnumerable of the wrapped IEnumerable</returns>
        public static IEnumerable<TElem> CallAfterEvaluation<TElem, TObj>(
            this IEnumerable<TElem> wrapped, 
            Action<TObj> callback, 
            params TObj[] objects)
        {
            if (wrapped == null) 
                throw new ArgumentNullException("wrapped");
            if (callback == null) 
                throw new ArgumentNullException("callback");

            foreach (TElem item in wrapped)
            {
                yield return item;
            }

            if (objects != null)
            {
                foreach (var obj in objects)
                {
                    callback(obj);
                }
            }
        }

        public static void EatException(Action action)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
            }
        }
    }
}
