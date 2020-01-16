using System;
using System.Linq;

namespace EventStore.Client {
	/// <summary>
	/// Represents an access control list for a stream
	/// </summary>
	public sealed class StreamAcl {
		/// <summary>
		/// Roles and users permitted to read the stream
		/// </summary>
		public string[] ReadRoles { get; }

		/// <summary>
		/// Roles and users permitted to write to the stream
		/// </summary>
		public string[] WriteRoles { get; }

		/// <summary>
		/// Roles and users permitted to delete the stream
		/// </summary>
		public string[] DeleteRoles { get; }

		/// <summary>
		/// Roles and users permitted to read stream metadata
		/// </summary>
		public string[] MetaReadRoles { get; }

		/// <summary>
		/// Roles and users permitted to write stream metadata
		/// </summary>
		public string[] MetaWriteRoles { get; }


		/// <summary>
		/// Creates a new Stream Access Control List
		/// </summary>
		/// <param name="readRole">Role and user permitted to read the stream</param>
		/// <param name="writeRole">Role and user permitted to write to the stream</param>
		/// <param name="deleteRole">Role and user permitted to delete the stream</param>
		/// <param name="metaReadRole">Role and user permitted to read stream metadata</param>
		/// <param name="metaWriteRole">Role and user permitted to write stream metadata</param>
		public StreamAcl(string readRole = default, string writeRole = default, string deleteRole = default,
			string metaReadRole = default, string metaWriteRole = default)
			: this(readRole == null ? null : new[] {readRole},
				writeRole == null ? null : new[] {writeRole},
				deleteRole == null ? null : new[] {deleteRole},
				metaReadRole == null ? null : new[] {metaReadRole},
				metaWriteRole == null ? null : new[] {metaWriteRole}) {
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="readRoles">Roles and users permitted to read the stream</param>
		/// <param name="writeRoles">Roles and users permitted to write to the stream</param>
		/// <param name="deleteRoles">Roles and users permitted to delete the stream</param>
		/// <param name="metaReadRoles">Roles and users permitted to read stream metadata</param>
		/// <param name="metaWriteRoles">Roles and users permitted to write stream metadata</param>
		public StreamAcl(string[] readRoles = default, string[] writeRoles = default, string[] deleteRoles = default,
			string[] metaReadRoles = default, string[] metaWriteRoles = default) {
			ReadRoles = readRoles;
			WriteRoles = writeRoles;
			DeleteRoles = deleteRoles;
			MetaReadRoles = metaReadRoles;
			MetaWriteRoles = metaWriteRoles;
		}

		private bool Equals(StreamAcl other) =>
			(ReadRoles ?? Array.Empty<string>()).SequenceEqual(other.ReadRoles ?? Array.Empty<string>()) &&
			(WriteRoles ?? Array.Empty<string>()).SequenceEqual(other.WriteRoles ?? Array.Empty<string>()) &&
			(DeleteRoles ?? Array.Empty<string>()).SequenceEqual(other.DeleteRoles ?? Array.Empty<string>()) &&
			(MetaReadRoles ?? Array.Empty<string>()).SequenceEqual(other.MetaReadRoles ?? Array.Empty<string>()) &&
			(MetaWriteRoles ?? Array.Empty<string>()).SequenceEqual(other.MetaWriteRoles ?? Array.Empty<string>());

		public override bool Equals(object obj) =>
			!ReferenceEquals(null, obj) &&
			(ReferenceEquals(this, obj) || obj.GetType() == GetType() && Equals((StreamAcl)obj));

		public static bool operator ==(StreamAcl left, StreamAcl right) => Equals(left, right);
		public static bool operator !=(StreamAcl left, StreamAcl right) => !Equals(left, right);
		public override int GetHashCode() =>
			HashCode.Hash.Combine(ReadRoles).Combine(WriteRoles).Combine(DeleteRoles).Combine(MetaReadRoles)
				.Combine(MetaWriteRoles);

		/// <summary>
		/// Returns a string that represents the current object.
		/// </summary>
		/// <returns>
		/// A string that represents the current object.
		/// </returns>
		/// <filterpriority>2</filterpriority>
		public override string ToString() =>
			$"Read: {(ReadRoles == null ? "<null>" : "[" + string.Join(",", ReadRoles) + "]")}, Write: {(WriteRoles == null ? "<null>" : "[" + string.Join(",", WriteRoles) + "]")}, Delete: {(DeleteRoles == null ? "<null>" : "[" + string.Join(",", DeleteRoles) + "]")}, MetaRead: {(MetaReadRoles == null ? "<null>" : "[" + string.Join(",", MetaReadRoles) + "]")}, MetaWrite: {(MetaWriteRoles == null ? "<null>" : "[" + string.Join(",", MetaWriteRoles) + "]")}";
	}
}
