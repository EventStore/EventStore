using System;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Represents an access control list for a stream
	/// </summary>
	public class StreamAcl {
		/// <summary>
		/// Roles and users permitted to read the stream
		/// </summary>
		public readonly string[] ReadRoles;

		/// <summary>
		/// Roles and users permitted to write to the stream
		/// </summary>
		public readonly string[] WriteRoles;

		/// <summary>
		/// Roles and users permitted to delete the stream
		/// </summary>
		public readonly string[] DeleteRoles;

		/// <summary>
		/// Roles and users permitted to read stream metadata
		/// </summary>
		public readonly string[] MetaReadRoles;

		/// <summary>
		/// Roles and users permitted to write stream metadata
		/// </summary>
		public readonly string[] MetaWriteRoles;

		/// <summary>
		/// Role or user permitted to read the stream
		/// </summary>
		public string ReadRole {
			get { return CheckAndReturnIfSingle(ReadRoles); }
		}

		/// <summary>
		/// Role or user permitted to write to the stream
		/// </summary>
		public string WriteRole {
			get { return CheckAndReturnIfSingle(WriteRoles); }
		}

		/// <summary>
		/// Role or user permitted to delete from the stream 
		/// </summary>
		public string DeleteRole {
			get { return CheckAndReturnIfSingle(DeleteRoles); }
		}

		/// <summary>
		/// Role or user permitted to read the stream metadata
		/// </summary>
		public string MetaReadRole {
			get { return CheckAndReturnIfSingle(MetaReadRoles); }
		}

		/// <summary>
		/// Role or user permitted to write to the stream metadata
		/// </summary>
		public string MetaWriteRole {
			get { return CheckAndReturnIfSingle(MetaWriteRoles); }
		}

		/// <summary>
		/// Creates a new Stream Access Control List
		/// </summary>
		/// <param name="readRole">Role and user permitted to read the stream</param>
		/// <param name="writeRole">Role and user permitted to write to the stream</param>
		/// <param name="deleteRole">Role and user permitted to delete the stream</param>
		/// <param name="metaReadRole">Role and user permitted to read stream metadata</param>
		/// <param name="metaWriteRole">Role and user permitted to write stream metadata</param>
		public StreamAcl(string readRole, string writeRole, string deleteRole, string metaReadRole,
			string metaWriteRole)
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
		public StreamAcl(string[] readRoles, string[] writeRoles, string[] deleteRoles, string[] metaReadRoles,
			string[] metaWriteRoles) {
			ReadRoles = readRoles;
			WriteRoles = writeRoles;
			DeleteRoles = deleteRoles;
			MetaReadRoles = metaReadRoles;
			MetaWriteRoles = metaWriteRoles;
		}

		/// <summary>
		/// Returns a string that represents the current object.
		/// </summary>
		/// <returns>
		/// A string that represents the current object.
		/// </returns>
		/// <filterpriority>2</filterpriority>
		public override string ToString() {
			return string.Format("Read: {0}, Write: {1}, Delete: {2}, MetaRead: {3}, MetaWrite: {4}",
				ReadRoles == null ? "<null>" : "[" + string.Join(",", ReadRoles) + "]",
				WriteRoles == null ? "<null>" : "[" + string.Join(",", WriteRoles) + "]",
				DeleteRoles == null ? "<null>" : "[" + string.Join(",", DeleteRoles) + "]",
				MetaReadRoles == null ? "<null>" : "[" + string.Join(",", MetaReadRoles) + "]",
				MetaWriteRoles == null ? "<null>" : "[" + string.Join(",", MetaWriteRoles) + "]");
		}

		private static string CheckAndReturnIfSingle(string[] roles) {
			if (roles.Length > 1)
				throw new ArgumentException(
					"Underlying stream ACL has multiple roles, which is not supported in old version of this API.");
			return roles.Length == 0 ? null : roles[0];
		}
	}
}
