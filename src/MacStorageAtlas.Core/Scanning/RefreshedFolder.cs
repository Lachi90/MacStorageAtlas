namespace MacStorageAtlas.Core.Scanning;

public sealed record RefreshedFolder
{
    public RefreshedFolder(string path, DateTimeOffset completedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Path = path;
        CompletedAt = completedAt;
    }

    public string Path { get; }

    public DateTimeOffset CompletedAt { get; }
}
