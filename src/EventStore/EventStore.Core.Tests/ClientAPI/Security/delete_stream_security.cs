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

using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Services;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Security
{
    [TestFixture, Category("LongRunning"), Category("Network")]
    public class delete_stream_security : AuthenticationTestBase
    {
        [Test, Category("LongRunning"), Category("Network")]
        public void delete_of_all_is_never_allowed()
        {
            Expect<AccessDeniedException>(() => DeleteStream("$all", null, null));
            Expect<AccessDeniedException>(() => DeleteStream("$all", "user1", "pa$$1"));
            Expect<AccessDeniedException>(() => DeleteStream("$all", "adm", "admpa$$"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_normal_no_acl_stream_with_no_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(StreamMetadata.Build());
            ExpectNoException(() => DeleteStream(streamId, null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_normal_no_acl_stream_with_existing_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(StreamMetadata.Build());
            ExpectNoException(() => DeleteStream(streamId, "user1", "pa$$1"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_normal_no_acl_stream_with_admin_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(StreamMetadata.Build());
            ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_normal_user_stream_with_no_user_is_not_allowed()
        {
            var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole("user1"));
            Expect<AccessDeniedException>(() => DeleteStream(streamId, null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_normal_user_stream_with_not_authorized_user_is_not_allowed()
        {
            var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole("user1"));
            Expect<AccessDeniedException>(() => DeleteStream(streamId, "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_normal_user_stream_with_authorized_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole("user1"));
            ExpectNoException(() => DeleteStream(streamId, "user1", "pa$$1"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_normal_user_stream_with_admin_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole("user1"));
            ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_normal_admin_stream_with_no_user_is_not_allowed()
        {
            var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole(SystemRoles.Admins));
            Expect<AccessDeniedException>(() => DeleteStream(streamId, null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_normal_admin_stream_with_existing_user_is_not_allowed()
        {
            var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole(SystemRoles.Admins));
            Expect<AccessDeniedException>(() => DeleteStream(streamId, "user1", "pa$$1"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_normal_admin_stream_with_admin_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole(SystemRoles.Admins));
            ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_normal_all_stream_with_no_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole(SystemRoles.All));
            ExpectNoException(() => DeleteStream(streamId, null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_normal_all_stream_with_existing_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole(SystemRoles.All));
            ExpectNoException(() => DeleteStream(streamId, "user1", "pa$$1"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_normal_all_stream_with_admin_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(StreamMetadata.Build().SetDeleteRole(SystemRoles.All));
            ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
        }

        // $-stream

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_system_no_acl_stream_with_no_user_is_not_allowed()
        {
            var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build());
            Expect<AccessDeniedException>(() => DeleteStream(streamId, null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_system_no_acl_stream_with_existing_user_is_not_allowed()
        {
            var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build());
            Expect<AccessDeniedException>(() => DeleteStream(streamId, "user1", "pa$$1"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_system_no_acl_stream_with_admin_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build());
            ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_system_user_stream_with_no_user_is_not_allowed()
        {
            var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build().SetDeleteRole("user1"));
            Expect<AccessDeniedException>(() => DeleteStream(streamId, null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_system_user_stream_with_not_authorized_user_is_not_allowed()
        {
            var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build().SetDeleteRole("user1"));
            Expect<AccessDeniedException>(() => DeleteStream(streamId, "user2", "pa$$2"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_system_user_stream_with_authorized_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build().SetDeleteRole("user1"));
            ExpectNoException(() => DeleteStream(streamId, "user1", "pa$$1"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_system_user_stream_with_admin_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build().SetDeleteRole("user1"));
            ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_system_admin_stream_with_no_user_is_not_allowed()
        {
            var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build().SetDeleteRole(SystemRoles.Admins));
            Expect<AccessDeniedException>(() => DeleteStream(streamId, null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_system_admin_stream_with_existing_user_is_not_allowed()
        {
            var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build().SetDeleteRole(SystemRoles.Admins));
            Expect<AccessDeniedException>(() => DeleteStream(streamId, "user1", "pa$$1"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_system_admin_stream_with_admin_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build().SetDeleteRole(SystemRoles.Admins));
            ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
        }


        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_system_all_stream_with_no_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build().SetDeleteRole(SystemRoles.All));
            ExpectNoException(() => DeleteStream(streamId, null, null));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_system_all_stream_with_existing_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build().SetDeleteRole(SystemRoles.All));
            ExpectNoException(() => DeleteStream(streamId, "user1", "pa$$1"));
        }

        [Test, Category("LongRunning"), Category("Network")]
        public void deleting_system_all_stream_with_admin_user_is_allowed()
        {
            var streamId = CreateStreamWithMeta(streamPrefix: "$", metadata: StreamMetadata.Build().SetDeleteRole(SystemRoles.All));
            ExpectNoException(() => DeleteStream(streamId, "adm", "admpa$$"));
        }
    }
}