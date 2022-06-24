using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace EventStore.Core.TransactionLog.Scavenging.Sqlite {
	public class SqliteBackend : IDisposable {
		private readonly SqliteConnection _connection;
		private SqliteTransaction _transaction;
		private const int SqliteDuplicateKeyError = 19;

		public const string CacheSize = "cache_size";
		public const string PageCount = "page_count";
		public const string PageSize = "page_size";
		public const string Synchronous = "synchronous";
		public const string JournalMode = "journal_mode";

		public SqliteBackend(SqliteConnection connection) {
			_connection = connection;
		}

		public void Dispose() {
			_connection.Close();
			_connection.Dispose();
		}

		public void InitializeDb(string createSql) {
			var createTableCmd = _connection.CreateCommand();
			createTableCmd.CommandText = createSql;
			createTableCmd.ExecuteNonQuery();
		}

		public SqliteCommand CreateCommand() {
			return _connection.CreateCommand();
		}

		public bool ExecuteReadAndDelete<TValue>(SqliteCommand selectCmd, SqliteCommand deleteCmd,
			Func<SqliteDataReader, TValue> getValue, out TValue value) {

			if (ExecuteSingleRead(selectCmd, getValue, out value)) {
				var affectedRows = ExecuteNonQuery(deleteCmd);
				
				if (affectedRows == 1) {
					return true;
				} 
				if (affectedRows > 1) {
					throw new SystemException("More values removed then expected!");
				}
			}

			value = default;
			return false;
		}

		public int ExecuteNonQuery(SqliteCommand cmd) {
			try {
				cmd.Transaction = _transaction;
				return cmd.ExecuteNonQuery();
			}
			catch (SqliteException e) when (e.SqliteErrorCode == SqliteDuplicateKeyError) {
				throw new ArgumentException();
			}
		}

		public bool ExecuteSingleRead<TValue>(SqliteCommand cmd, Func<SqliteDataReader, TValue> getValue, out TValue value) {
			cmd.Transaction = _transaction;
			using (var reader = cmd.ExecuteReader()) {
				if (reader.Read()) {
					value = getValue(reader);
					return true;
				}
			}
			
			value = default;
			return false;
		}

		public IEnumerable<KeyValuePair<TKey, TValue>> ExecuteReader<TKey, TValue>(SqliteCommand cmd,
			Func<SqliteDataReader, KeyValuePair<TKey, TValue>> toValue) {

			cmd.Transaction = _transaction;
			using (var reader = cmd.ExecuteReader()) {
				while (reader.Read()) {
					yield return toValue(reader);
				}
			}
		}

		public static T GetNullableFieldValue<T>(int ordinal, SqliteDataReader reader) {
			if (!reader.IsDBNull(ordinal)) {
				return reader.GetFieldValue<T>(ordinal);
			}

			return default;
		}

		public static TimeSpan? GetTimeSpanFromSeconds(int ordinal, SqliteDataReader reader) {
			if (!reader.IsDBNull(ordinal)) {
				return TimeSpan.FromSeconds(reader.GetFieldValue<long>(ordinal));
			}

			return null;
		}
		
		public SqliteTransaction BeginTransaction() {
			_transaction = _connection.BeginTransaction();
			return _transaction;
		}

		public void ClearTransaction() {
			_transaction = null;
		}
		
		public Stats GetStats() {
			var databaseSize = int.Parse(GetPragmaValue(PageSize)) * int.Parse(GetPragmaValue(PageCount));
			var cacheSizeInKibiBytes = -1 * int.Parse(GetPragmaValue(CacheSize));
			var cacheSizeInBytes = cacheSizeInKibiBytes * 1024;
			return new Stats(raw.sqlite3_memory_used(), databaseSize, cacheSizeInBytes);
		}

		public void SetPragmaValue(string name, string value) {
			using (var cmd = _connection.CreateCommand()) {
				cmd.CommandText = $"PRAGMA {name}={value}";
				cmd.ExecuteNonQuery();
			}
		}
		
		public string GetPragmaValue(string name) {
			var cmd = _connection.CreateCommand();
			cmd.CommandText = "PRAGMA " + name;
			var result = cmd.ExecuteScalar();
			
			if (result != null) {
				return result.ToString();
			}

			throw new Exception("Unexpected pragma result!");
		}
		
		public class Stats {
			public Stats(long memoryUsage, int databaseSize, int cacheSize) {
				MemoryUsage = memoryUsage;
				DatabaseSize = databaseSize;
				CacheSize = cacheSize;
			}

			public long MemoryUsage { get; }
			public long DatabaseSize { get; }
			public long CacheSize { get; }

			public string PrettyPrint() {
				var dbSizeMb = (float)DatabaseSize / 1_000_000;
				var cacheSizeMb = (float)CacheSize / 1_000_000;
				var memSizeMb = (float)MemoryUsage / 1_000_000;
				return
					$"ScavengeState size: {dbSizeMb:N2} MB. " +
					$"Cache size: {cacheSizeMb:N2} MB. " +
					$"Memory usage: {memSizeMb:N2} MB";
			}
		}
	}
}
