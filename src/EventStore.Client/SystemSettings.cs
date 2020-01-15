namespace EventStore.Client {
	public sealed class SystemSettings {
		/// <summary>
		/// Default access control list for new user streams.
		/// </summary>
		public StreamAcl UserStreamAcl { get; }

		/// <summary>
		/// Default access control list for new system streams.
		/// </summary>
		public StreamAcl SystemStreamAcl { get; }

		/// <summary>
		/// Constructs a new <see cref="SystemSettings"/>.
		/// </summary>
		/// <param name="userStreamAcl"></param>
		/// <param name="systemStreamAcl"></param>
		public SystemSettings(StreamAcl userStreamAcl = default, StreamAcl systemStreamAcl = default) {
			UserStreamAcl = userStreamAcl;
			SystemStreamAcl = systemStreamAcl;
		}

		private bool Equals(SystemSettings other)
			=> Equals(UserStreamAcl, other.UserStreamAcl) && Equals(SystemStreamAcl, other.SystemStreamAcl);

		public override bool Equals(object obj)
			=> ReferenceEquals(this, obj) || obj is SystemSettings other && Equals(other);

		public static bool operator ==(SystemSettings left, SystemSettings right) => Equals(left, right);
		public static bool operator !=(SystemSettings left, SystemSettings right) => !Equals(left, right);
		public override int GetHashCode() => HashCode.Hash.Combine(UserStreamAcl?.GetHashCode())
			.Combine(SystemStreamAcl?.GetHashCode());
	}
}
