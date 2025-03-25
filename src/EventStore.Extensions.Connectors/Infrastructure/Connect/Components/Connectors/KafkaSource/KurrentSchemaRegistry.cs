using System.Buffers.Binary;
using System.Runtime.Serialization;

namespace Kurrent.Connectors.Kafka;

internal static class KurrentSchemaRegistry {
    /// <summary>
    /// Serialization format:
    ///       byte 0:           A magic byte that identifies this as a message with
    ///                         Confluent Platform framing.
    ///       bytes 1-4:        Unique global id of the schema associated with
    ///                         the data (as registered in Confluent Schema Registry),
    ///                         big endian.
    /// </summary>
    public static int ParseSchemaId(ReadOnlyMemory<byte> data) {
        try {
            return BinaryPrimitives.ReadInt32BigEndian(data.Slice(1, 4).Span);
        }
        catch (Exception ex) {
            throw new SerializationException("Failed to parse Confluent Platform Schema Id!", ex);
        }
    }

    /// <summary>
    /// Extracts the actual message payload from Confluent Platform framing.
    /// Skips the magic byte (byte 0) and schema ID (bytes 1-4).
    /// </summary>
    /// <param name="data">The complete message data with Confluent Platform framing</param>
    /// <returns>The message payload without the framing metadata</returns>
    public static ReadOnlyMemory<byte> ExtractMessagePayload(ReadOnlyMemory<byte> data) {
        try {
            return data[5..];
        }
        catch (Exception ex) {
            throw new SerializationException("Failed to extract message payload from Confluent Platform format!", ex);
        }
    }

    /// <summary>
    ///     Magic byte that identifies a message with Confluent Platform framing.
    /// </summary>
    public const byte MagicByte = 0;
}