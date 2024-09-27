// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.TransactionLog.Scavenging.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Scavenge.Sqlite {
	public class SqliteTypeMappingTests {
		[Fact]
		public void can_map() {
			Assert.Equal(SqliteType.Integer, SqliteTypeMapping.Map<int>());
			Assert.Equal(SqliteType.Real, SqliteTypeMapping.Map<float>());
			Assert.Equal(SqliteType.Integer, SqliteTypeMapping.Map<long>());
			Assert.Equal(SqliteType.Integer, SqliteTypeMapping.Map<ulong>());
			Assert.Equal(SqliteType.Text, SqliteTypeMapping.Map<string>());
		}

		[Fact]
		public void can_get_type_name() {
			Assert.Equal("INTEGER", SqliteTypeMapping.GetTypeName<int>());
			Assert.Equal("REAL", SqliteTypeMapping.GetTypeName<float>());
			Assert.Equal("INTEGER", SqliteTypeMapping.GetTypeName<long>());
			Assert.Equal("INTEGER", SqliteTypeMapping.GetTypeName<ulong>());
			Assert.Equal("TEXT", SqliteTypeMapping.GetTypeName<string>());
		}
	}
}
