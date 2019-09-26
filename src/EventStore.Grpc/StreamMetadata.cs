using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text.Json;

namespace EventStore.Grpc {
	public struct StreamMetadata : IEquatable<StreamMetadata> {
		public TimeSpan? MaxAge { get; }
		public StreamRevision? TruncateBefore { get; }
		public TimeSpan? CacheControl { get; }
		public StreamAcl Acl { get; }
		public int? MaxCount { get; }
		public JsonDocument CustomMetadata { get; }


		public StreamMetadata(
			int? maxCount = default,
			TimeSpan? maxAge = default,
			StreamRevision? truncateBefore = default,
			TimeSpan? cacheControl = default,
			StreamAcl acl = null,
			JsonDocument customMetadata = null) : this() {
			if (maxCount <= 0) {
				throw new ArgumentOutOfRangeException(nameof(maxCount));
			}

			if (maxAge <= TimeSpan.Zero) {
				throw new ArgumentOutOfRangeException(nameof(maxAge));
			}

			if (cacheControl <= TimeSpan.Zero) {
				throw new ArgumentOutOfRangeException(nameof(cacheControl));
			}

			MaxAge = maxAge;
			TruncateBefore = truncateBefore;
			CacheControl = cacheControl;
			Acl = acl;
			MaxCount = maxCount;
			CustomMetadata = customMetadata ?? JsonDocument.Parse("{}");
		}

		public bool Equals(StreamMetadata other) => Nullable.Equals(MaxAge, other.MaxAge) &&
		                                            Nullable.Equals(TruncateBefore, other.TruncateBefore) &&
		                                            Nullable.Equals(CacheControl, other.CacheControl) &&
		                                            Equals(Acl, other.Acl) && MaxCount == other.MaxCount &&
		                                            string.Equals(
			                                            CustomMetadata?.RootElement.GetRawText(),
			                                            other.CustomMetadata?.RootElement.GetRawText());

		public override bool Equals(object obj) => obj is StreamMetadata other && other.Equals(this);

		public override int GetHashCode() => HashCode.Hash.Combine(MaxAge).Combine(TruncateBefore).Combine(CacheControl)
			.Combine(Acl?.GetHashCode()).Combine(MaxCount);

		public static bool operator ==(StreamMetadata left, StreamMetadata right) => Equals(left, right);
		public static bool operator !=(StreamMetadata left, StreamMetadata right) => !Equals(left, right);
	}
}
