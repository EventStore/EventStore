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
using EventStore.Core.Services.Transport.Tcp;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Tcp
{
    [TestFixture]
    public class core_tcp_package
    {
        [Test]
        public void should_throw_argument_null_exception_when_created_as_authorized_but_login_not_provided()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TcpPackage(TcpCommand.BadRequest, TcpFlags.Authenticated, Guid.NewGuid(), null, "pa$$", new byte[] { 1, 2, 3 }));
        }

        [Test]
        public void should_throw_argument_null_exception_when_created_as_authorized_but_password_not_provided()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TcpPackage(TcpCommand.BadRequest, TcpFlags.Authenticated, Guid.NewGuid(), "login", null, new byte[] { 1, 2, 3 }));
        }

        [Test]
        public void should_throw_argument_exception_when_created_as_not_authorized_but_login_is_provided()
        {
            Assert.Throws<ArgumentException>(() =>
                new TcpPackage(TcpCommand.BadRequest, TcpFlags.None, Guid.NewGuid(), "login", null, new byte[] { 1, 2, 3 }));
        }

        [Test]
        public void should_throw_argument_exception_when_created_as_not_authorized_but_password_is_provided()
        {
            Assert.Throws<ArgumentException>(() =>
                new TcpPackage(TcpCommand.BadRequest, TcpFlags.None, Guid.NewGuid(), null, "pa$$", new byte[] { 1, 2, 3 }));
        }

        [Test]
        public void not_authorized_with_data_should_serialize_and_deserialize_correctly()
        {
            var corrId = Guid.NewGuid();
            var refPkg = new TcpPackage(TcpCommand.BadRequest, TcpFlags.None, corrId, null, null, new byte[] { 1, 2, 3});
            var bytes = refPkg.AsArraySegment();

            var pkg = TcpPackage.FromArraySegment(bytes);
            Assert.AreEqual(TcpCommand.BadRequest, pkg.Command);
            Assert.AreEqual(TcpFlags.None, pkg.Flags);
            Assert.AreEqual(corrId, pkg.CorrelationId);
            Assert.AreEqual(null, pkg.Login);
            Assert.AreEqual(null, pkg.Password);

            Assert.AreEqual(3, pkg.Data.Count);
            Assert.AreEqual(1, pkg.Data.Array[pkg.Data.Offset + 0]);
            Assert.AreEqual(2, pkg.Data.Array[pkg.Data.Offset + 1]);
            Assert.AreEqual(3, pkg.Data.Array[pkg.Data.Offset + 2]);
        }

        [Test]
        public void not_authorized_with_empty_data_should_serialize_and_deserialize_correctly()
        {
            var corrId = Guid.NewGuid();
            var refPkg = new TcpPackage(TcpCommand.BadRequest, TcpFlags.None, corrId, null, null, new byte[0]);
            var bytes = refPkg.AsArraySegment();

            var pkg = TcpPackage.FromArraySegment(bytes);
            Assert.AreEqual(TcpCommand.BadRequest, pkg.Command);
            Assert.AreEqual(TcpFlags.None, pkg.Flags);
            Assert.AreEqual(corrId, pkg.CorrelationId);
            Assert.AreEqual(null, pkg.Login);
            Assert.AreEqual(null, pkg.Password);

            Assert.AreEqual(0, pkg.Data.Count);
        }

        [Test]
        public void authorized_with_data_should_serialize_and_deserialize_correctly()
        {
            var corrId = Guid.NewGuid();
            var refPkg = new TcpPackage(TcpCommand.BadRequest, TcpFlags.Authenticated, corrId, "login", "pa$$", new byte[] { 1, 2, 3 });
            var bytes = refPkg.AsArraySegment();

            var pkg = TcpPackage.FromArraySegment(bytes);
            Assert.AreEqual(TcpCommand.BadRequest, pkg.Command);
            Assert.AreEqual(TcpFlags.Authenticated, pkg.Flags);
            Assert.AreEqual(corrId, pkg.CorrelationId);
            Assert.AreEqual("login", pkg.Login);
            Assert.AreEqual("pa$$", pkg.Password);

            Assert.AreEqual(3, pkg.Data.Count);
            Assert.AreEqual(1, pkg.Data.Array[pkg.Data.Offset + 0]);
            Assert.AreEqual(2, pkg.Data.Array[pkg.Data.Offset + 1]);
            Assert.AreEqual(3, pkg.Data.Array[pkg.Data.Offset + 2]);
        }

        [Test]
        public void authorized_with_empty_data_should_serialize_and_deserialize_correctly()
        {
            var corrId = Guid.NewGuid();
            var refPkg = new TcpPackage(TcpCommand.BadRequest, TcpFlags.Authenticated, corrId, "login", "pa$$", new byte[0]);
            var bytes = refPkg.AsArraySegment();

            var pkg = TcpPackage.FromArraySegment(bytes);
            Assert.AreEqual(TcpCommand.BadRequest, pkg.Command);
            Assert.AreEqual(TcpFlags.Authenticated, pkg.Flags);
            Assert.AreEqual(corrId, pkg.CorrelationId);
            Assert.AreEqual("login", pkg.Login);
            Assert.AreEqual("pa$$", pkg.Password);

            Assert.AreEqual(0, pkg.Data.Count);
        }

        [Test]
        public void should_throw_argument_exception_on_serialization_when_login_too_long()
        {
            var pkg = new TcpPackage(TcpCommand.BadRequest, TcpFlags.Authenticated, Guid.NewGuid(), new string('*', 256), "pa$$", new byte[] { 1, 2, 3 });
            Assert.Throws<ArgumentException>(() => pkg.AsByteArray());
        }

        [Test]
        public void should_throw_argument_exception_on_serialization_when_password_too_long()
        {
            var pkg = new TcpPackage(TcpCommand.BadRequest, TcpFlags.Authenticated, Guid.NewGuid(), "login", new string('*', 256), new byte[] { 1, 2, 3 });
            Assert.Throws<ArgumentException>(() => pkg.AsByteArray());
        }

    }
}
