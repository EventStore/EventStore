﻿// Copyright (c) 2012, Event Store LLP
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

namespace EventStore.Common.Utils
{
    public static class Ensure
    {
        public static void NotNull<T>(T argument, string argumentName) where T : class 
        {
            if (argument == null)
                throw new ArgumentNullException(argumentName);
        }

        public static void NotNullOrEmpty(string argument, string argumentName)
        {
            if (string.IsNullOrEmpty(argument))
                throw new ArgumentNullException(argument, argumentName);
        }

        public static void Positive(int number, string argumentName)
        {
            if (number <= 0)
                throw new ArgumentOutOfRangeException(argumentName, argumentName + " should be positive.");
        }

        public static void Positive(long number, string argumentName)
        {
            if (number <= 0)
                throw new ArgumentOutOfRangeException(argumentName, argumentName + " should be positive.");
        }

        public static void Nonnegative(long number, string argumentName)
        {
            if (number < 0)
                throw new ArgumentOutOfRangeException(argumentName, argumentName + " should be non negative.");
        }

        public static void Nonnegative(int number, string argumentName)
        {
            if (number < 0)
                throw new ArgumentOutOfRangeException(argumentName, argumentName + " should be non negative.");
        }

        public static void NotEmptyGuid(Guid guid, string argumentName)
        {
            if (Guid.Empty == guid)
                throw new ArgumentException(argumentName, argumentName + " shoud be non-empty GUID.");
        }

        public static void Equal(int expected, int actual, string argumentName)
        {
            if (expected != actual)
                throw new ArgumentException(string.Format("{0} expected value: {1}, actual value: {2}", argumentName, expected, actual));
        }

        public static void Equal(long expected, long actual, string argumentName)
        {
            if (expected != actual)
                throw new ArgumentException(string.Format("{0} expected value: {1}, actual value: {2}", argumentName, expected, actual));
        }

        public static void Equal(bool expected, bool actual, string argumentName)
        {
            if (expected != actual)
                throw new ArgumentException(string.Format("{0} expected value: {1}, actual value: {2}", argumentName, expected, actual));
        }
    }
}