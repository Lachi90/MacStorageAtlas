namespace MacStorageAtlas.Core.Filtering;

public sealed record FilterPreset(string Name, DiskItemFilter Filter, bool IsBuiltIn = false);
