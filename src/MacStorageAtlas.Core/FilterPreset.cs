namespace MacStorageAtlas.Core;

public sealed record FilterPreset(string Name, DiskItemFilter Filter, bool IsBuiltIn = false);
