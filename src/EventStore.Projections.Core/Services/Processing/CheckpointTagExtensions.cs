using System;
using System.Collections.Generic;
using System.IO;
using EventStore.Core.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Projections.Core.Services.Processing {
	public struct CheckpointTagVersion {
		public ProjectionVersion Version;
		public int SystemVersion;
		public CheckpointTag Tag;
		public Dictionary<string, JToken> ExtraMetadata;

		public CheckpointTag AdjustBy(PositionTagger tagger, ProjectionVersion version) {
			if (SystemVersion == ProjectionsSubsystem.VERSION && Version.Version == version.Version
			                                                  && Version.ProjectionId == version.ProjectionId)
				return Tag;

			return tagger.AdjustTag(Tag);
		}
	}

	public static class CheckpointTagExtensions {
		public static CheckpointTag ParseCheckpointTagJson(this string source) {
			if (string.IsNullOrEmpty(source))
				return null;
			var reader = new JsonTextReader(new StringReader(source));
			return CheckpointTag.FromJson(reader, default(ProjectionVersion)).Tag;
		}

		public static CheckpointTag ParseCheckpointTagJson(this byte[] source) {
			if (source == null || source.Length == 0)
				return null;
			var reader = new JsonTextReader(new StreamReader(new MemoryStream(source)));
			return CheckpointTag.FromJson(reader, default(ProjectionVersion)).Tag;
		}

		public static CheckpointTag ParseCheckpointTagJson(this ReadOnlyMemory<byte> source) {
			if (source.Length == 0)
				return null;
			var reader = new JsonTextReader(new StreamReader(new MemoryStream(source.ToArray())));
			return CheckpointTag.FromJson(reader, default(ProjectionVersion)).Tag;
		}

		public static CheckpointTagVersion ParseCheckpointTagVersionExtraJson(this byte[] source,
			ProjectionVersion current) {
			if (source == null || source.Length == 0)
				return new CheckpointTagVersion
					{Version = new ProjectionVersion(current.ProjectionId, 0, 0), Tag = null};
			var reader = new JsonTextReader(new StreamReader(new MemoryStream(source)));
			return CheckpointTag.FromJson(reader, current);
		}

		public static CheckpointTagVersion ParseCheckpointTagVersionExtraJson(this ReadOnlyMemory<byte> source,
			ProjectionVersion current) {
			if (source.Length == 0)
				return new CheckpointTagVersion { Version = new ProjectionVersion(current.ProjectionId, 0, 0), Tag = null };
			var reader = new JsonTextReader(new StreamReader(new MemoryStream(source.ToArray())));
			return CheckpointTag.FromJson(reader, current);
		}

		public static CheckpointTagVersion ParseCheckpointTagVersionExtraJson(this string source,
			ProjectionVersion current) {
			if (string.IsNullOrEmpty(source))
				return new CheckpointTagVersion
					{Version = new ProjectionVersion(current.ProjectionId, 0, 0), Tag = null};
			var reader = new JsonTextReader(new StringReader(source));
			return CheckpointTag.FromJson(reader, current);
		}

		public static Dictionary<string, JToken> ParseCheckpointExtraJson(this string source) {
			if (string.IsNullOrEmpty(source))
				return null;
			var reader = new JsonTextReader(new StringReader(source));
			return CheckpointTag.FromJson(reader, default(ProjectionVersion)).ExtraMetadata;
		}

		public static string ParseCheckpointTagCorrelationId(this string source) {
			try {
				if (string.IsNullOrEmpty(source))
					return null;
				var reader = new JsonTextReader(new StringReader(source));
				if (!reader.Read()) return null;
				if (reader.TokenType != JsonToken.StartObject) return null;
				while (true) {
					CheckpointTag.Check(reader.Read(), reader);
					if (reader.TokenType == JsonToken.EndObject)
						break;
					if (reader.TokenType != JsonToken.PropertyName) return null;
					var name = (string)reader.Value;
					switch (name) {
						default:
							if (!reader.Read()) return null;
							var jToken = JToken.ReadFrom(reader);
							if (name == "$correlationId")
								return jToken.ToString();
							break;
					}
				}

				return null;
			} catch (JsonReaderException) {
				return null;
			}
		}
	}
}
