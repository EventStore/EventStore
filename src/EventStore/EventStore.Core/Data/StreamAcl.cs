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

namespace EventStore.Core.Data
{
    public class StreamAcl
    {
        public readonly string[] ReadRoles;
        public readonly string[] WriteRoles;
        public readonly string[] DeleteRoles;
        public readonly string[] MetaReadRoles;
        public readonly string[] MetaWriteRoles;

        public StreamAcl(string readRole, string writeRole, string deleteRole, string metaReadRole, string metaWriteRole)
                : this(readRole == null ? null : new[]{readRole},
                       writeRole == null ? null : new[]{writeRole},
                       deleteRole == null ? null : new[]{deleteRole},
                       metaReadRole == null ? null : new[]{metaReadRole},
                       metaWriteRole == null ? null : new[]{metaWriteRole})
        {
        }

        public StreamAcl(string[] readRoles, string[] writeRoles, string[] deleteRoles, string[] metaReadRoles, string[] metaWriteRoles)
        {
            ReadRoles = readRoles;
            WriteRoles = writeRoles;
            DeleteRoles = deleteRoles;
            MetaReadRoles = metaReadRoles;
            MetaWriteRoles = metaWriteRoles;
        }

        public override string ToString()
        {
            return string.Format("Read: {0}, Write: {1}, Delete: {2}, MetaRead: {3}, MetaWrite: {4}",
                                 ReadRoles == null ? "<null>" : "[" + string.Join(",", ReadRoles) + "]",
                                 WriteRoles == null ? "<null>" : "[" + string.Join(",", WriteRoles) + "]",
                                 DeleteRoles == null ? "<null>" : "[" + string.Join(",", DeleteRoles) + "]",
                                 MetaReadRoles == null ? "<null>" : "[" + string.Join(",", MetaReadRoles) + "]",
                                 MetaWriteRoles == null ? "<null>" : "[" + string.Join(",", MetaWriteRoles) + "]");
        }
    }
}