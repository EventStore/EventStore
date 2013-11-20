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

namespace EventStore.Core.Data
{
    public class UserData
    {
        public readonly string LoginName;
        public readonly string FullName;
        public readonly string Salt;
        public readonly string Hash;
        public readonly bool Disabled;
        public readonly string[] Groups;

        public UserData(string loginName, string fullName, string[] groups, string hash, string salt, bool disabled)
        {
            LoginName = loginName;
            FullName = fullName;
            Groups = groups;
            Salt = salt;
            Hash = hash;
            Disabled = disabled;
        }

        public UserData SetFullName(string fullName)
        {
            return new UserData(LoginName, fullName, Groups, Hash, Salt, Disabled);
        }

        public UserData SetGroups(string[] groups)
        {
            return new UserData(LoginName, FullName, groups, Hash, Salt, Disabled);
        }

        public UserData SetPassword(string hash, string salt)
        {
            return new UserData(LoginName, FullName, Groups, hash, salt, Disabled);
        }

        public UserData SetEnabled()
        {
            return new UserData(LoginName, FullName, Groups, Hash, Salt, disabled: false);
        }

        public UserData SetDisabled()
        {
            return new UserData(LoginName, FullName, Groups, Hash, Salt, disabled: true);
        }
    }
}
