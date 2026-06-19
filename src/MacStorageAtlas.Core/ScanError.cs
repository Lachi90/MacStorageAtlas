namespace MacStorageAtlas.Core;

public sealed record ScanError(string Path, string Message, string ExceptionType);
