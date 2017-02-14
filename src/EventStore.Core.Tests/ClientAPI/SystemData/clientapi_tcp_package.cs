﻿using System;
using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.SystemData
{
    [TestFixture, Category("ClientAPI")]
    public class clientapi_tcp_package
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
