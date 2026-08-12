namespace MacStorageAtlas.Core.Scanning;

public sealed record ScanError(string Path, string Message, string ExceptionType);
