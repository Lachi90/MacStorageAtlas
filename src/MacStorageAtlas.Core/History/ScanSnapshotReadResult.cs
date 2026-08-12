namespace MacStorageAtlas.Core.History;

public enum ScanSnapshotReadStatus
{
    Ok = 0,
    UnsupportedSchemaVersion = 1,
    Unreadable = 2
}

public sealed record ScanSnapshotReadResult<TPayload>
    where TPayload : class
{
    private ScanSnapshotReadResult(
        ScanSnapshotReadStatus status,
        TPayload? payload,
        int? schemaVersion,
        string? message)
    {
        Status = status;
        Payload = payload;
        SchemaVersion = schemaVersion;
        Message = message;
    }

    public ScanSnapshotReadStatus Status { get; }

    public TPayload? Payload { get; }

    public int? SchemaVersion { get; }

    public string? Message { get; }

    public bool IsOk => Status == ScanSnapshotReadStatus.Ok;

    public static ScanSnapshotReadResult<TPayload> Ok(TPayload payload, int schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return new ScanSnapshotReadResult<TPayload>(
            ScanSnapshotReadStatus.Ok,
            payload,
            schemaVersion,
            message: null);
    }

    public static ScanSnapshotReadResult<TPayload> UnsupportedSchemaVersion(
        int schemaVersion) =>
        new(
            ScanSnapshotReadStatus.UnsupportedSchemaVersion,
            payload: null,
            schemaVersion,
            $"Snapshot schema version {schemaVersion} cannot be read by this version "
            + $"of MacStorageAtlas, which reads version "
            + $"{ScanSnapshotSchema.CurrentVersion}.");

    public static ScanSnapshotReadResult<TPayload> Unreadable(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new ScanSnapshotReadResult<TPayload>(
            ScanSnapshotReadStatus.Unreadable,
            payload: null,
            schemaVersion: null,
            message);
    }
}
