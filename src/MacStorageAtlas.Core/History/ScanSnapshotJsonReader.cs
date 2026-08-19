using System.IO.Compression;
using System.Text.Json;
using MacStorageAtlas.Core.Export;
using MacStorageAtlas.Core.Scanning;
using MacStorageAtlas.Core.Serialization;

namespace MacStorageAtlas.Core.History;

public static class ScanSnapshotJsonReader
{
    private const int InitialPrefixBytes = 8 * 1024;
    private const int MaximumPrefixBytes = 1024 * 1024;

    public static ScanSnapshotReadResult<ScanSnapshotDocument> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var decompressor = new GZipStream(
                stream,
                CompressionMode.Decompress,
                leaveOpen: true);
            using var document = JsonDocument.Parse(decompressor);

            var root = document.RootElement;
            var schemaVersion = root.GetProperty("schemaVersion").GetInt32();

            if (!ScanSnapshotSchema.IsSupported(schemaVersion))
            {
                return ScanSnapshotReadResult<ScanSnapshotDocument>
                    .UnsupportedSchemaVersion(schemaVersion);
            }

            var metadata = ReadMetadata(root.GetProperty("scan"), schemaVersion);
            var errors = ScanDocumentJson.ReadErrors(root);
            var items = root.GetProperty("items")
                .EnumerateArray()
                .Select(ScanDocumentJson.ReadRow)
                .ToArray();

            return ScanSnapshotReadResult<ScanSnapshotDocument>.Ok(
                new ScanSnapshotDocument(metadata, items, errors),
                schemaVersion);
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            return ScanSnapshotReadResult<ScanSnapshotDocument>.Unreadable(
                exception.Message);
        }
    }

    public static ScanSnapshotReadResult<ScanSnapshotDescriptor> ReadDescriptor(
        Stream stream,
        long storedSizeBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegative(storedSizeBytes);

        try
        {
            using var decompressor = new GZipStream(
                stream,
                CompressionMode.Decompress,
                leaveOpen: true);

            var buffer = new byte[InitialPrefixBytes];
            var length = 0;
            var endOfStream = false;

            while (true)
            {
                var prefix = buffer.AsSpan(0, length);

                if (TryReadSchemaVersion(prefix, endOfStream, out var declaredVersion)
                    && !ScanSnapshotSchema.IsSupported(declaredVersion))
                {
                    return ScanSnapshotReadResult<ScanSnapshotDescriptor>
                        .UnsupportedSchemaVersion(declaredVersion);
                }

                if (TryReadHeader(
                        prefix,
                        endOfStream,
                        out var schemaVersion,
                        out var scanStart,
                        out var scanLength))
                {
                    using var scan = JsonDocument.Parse(
                        buffer.AsMemory(scanStart, scanLength));

                    return ScanSnapshotReadResult<ScanSnapshotDescriptor>.Ok(
                        new ScanSnapshotDescriptor(
                            ReadMetadata(scan.RootElement, schemaVersion),
                            storedSizeBytes),
                        schemaVersion);
                }

                if (endOfStream)
                {
                    return ScanSnapshotReadResult<ScanSnapshotDescriptor>.Unreadable(
                        "The snapshot ended before its scan metadata was complete.");
                }

                if (length == buffer.Length)
                {
                    if (buffer.Length >= MaximumPrefixBytes)
                    {
                        return ScanSnapshotReadResult<ScanSnapshotDescriptor>.Unreadable(
                            "The snapshot's scan metadata exceeds the readable prefix.");
                    }

                    Array.Resize(
                        ref buffer,
                        Math.Min(buffer.Length * 2, MaximumPrefixBytes));
                }

                var read = decompressor.Read(buffer, length, buffer.Length - length);

                if (read == 0)
                {
                    endOfStream = true;
                }
                else
                {
                    length += read;
                }
            }
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            return ScanSnapshotReadResult<ScanSnapshotDescriptor>.Unreadable(
                exception.Message);
        }
    }

    private static bool TryReadSchemaVersion(
        ReadOnlySpan<byte> buffer,
        bool isFinalBlock,
        out int schemaVersion)
    {
        schemaVersion = 0;

        var reader = new Utf8JsonReader(buffer, isFinalBlock, state: default);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return false;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var isSchemaVersion = reader.ValueTextEquals("schemaVersion"u8);

            if (!reader.Read())
            {
                return false;
            }

            if (isSchemaVersion)
            {
                return reader.TryGetInt32(out schemaVersion);
            }

            if (!reader.TrySkip())
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryReadHeader(
        ReadOnlySpan<byte> buffer,
        bool isFinalBlock,
        out int schemaVersion,
        out int scanStart,
        out int scanLength)
    {
        schemaVersion = 0;
        scanStart = 0;
        scanLength = 0;

        var reader = new Utf8JsonReader(buffer, isFinalBlock, state: default);
        var hasSchemaVersion = false;

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return false;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var isSchemaVersion = reader.ValueTextEquals("schemaVersion"u8);
            var isScan = reader.ValueTextEquals("scan"u8);

            if (!reader.Read())
            {
                return false;
            }

            if (isSchemaVersion)
            {
                if (!reader.TryGetInt32(out schemaVersion))
                {
                    return false;
                }

                hasSchemaVersion = true;
                continue;
            }

            if (isScan)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    return false;
                }

                var start = (int)reader.TokenStartIndex;

                if (!reader.TrySkip())
                {
                    return false;
                }

                scanStart = start;
                scanLength = (int)reader.BytesConsumed - start;
                return hasSchemaVersion;
            }

            if (!reader.TrySkip())
            {
                return false;
            }
        }

        return false;
    }

    private static ScanSnapshotMetadata ReadMetadata(JsonElement scan, int schemaVersion)
    {
        var measurementMode =
            ScanDocumentJson.ReadEnum<StorageMeasurementMode>(scan, "measurementMode");

        return new ScanSnapshotMetadata(
            scan.GetProperty("snapshotId").GetString()!,
            ScanExportValues.ParseTimestamp(
                scan.GetProperty("capturedAt").GetString())!.Value,
            scan.GetProperty("rootPath").GetString()!,
            ScanExportValues.ParseTimestamp(
                scan.GetProperty("completedAt").GetString())!.Value,
            ScanDocumentJson.ReadOptions(scan, measurementMode),
            measurementMode,
            ScanDocumentJson.ReadEnum<CloneAccountingCoverage>(
                scan,
                "cloneAccountingCoverage"),
            scan.GetProperty("itemCount").GetInt64(),
            scan.GetProperty("totalCountedSizeBytes").GetInt64(),
            scan.GetProperty("errorCount").GetInt64(),
            ScanDocumentJson.ReadEnum<ScanCompleteness>(scan, "completeness"))
        {
            SchemaVersion = schemaVersion
        };
    }

    private static bool IsUnreadable(Exception exception) =>
        exception is JsonException
            or InvalidDataException
            or IOException
            or KeyNotFoundException
            or FormatException
            or OverflowException
            or ArgumentException;
}
