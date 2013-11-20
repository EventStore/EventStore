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

using System.Security.Cryptography;
using EventStore.Core.Authentication;

namespace EventStore.Core.Services.Transport.Http.Authentication
{
    public class Rfc2898PasswordHashAlgorithm : PasswordHashAlgorithm
    {
        private const int HashSize = 20;
        private const int SaltSize = 16;

        public override void Hash(string password, out string hash, out string salt)
        {
            var salt_ = new byte[SaltSize];
            var randomProvider = new RNGCryptoServiceProvider();
            randomProvider.GetBytes(salt_);
            var hash_ = new Rfc2898DeriveBytes(password, salt_).GetBytes(HashSize);
            hash = System.Convert.ToBase64String(hash_);
            salt = System.Convert.ToBase64String(salt_);
        }

        public override bool Verify(string password, string hash, string salt)
        {
            var salt_ = System.Convert.FromBase64String(salt);

            var newHash = System.Convert.ToBase64String(new Rfc2898DeriveBytes(password, salt_).GetBytes(HashSize));

            return hash == newHash;
        }
    }
}
