namespace MacStorageAtlas.Core.Filtering;

public readonly record struct DiskItemFilterValidation(bool IsValid, string? Message)
{
    public static DiskItemFilterValidation Valid { get; } = new(true, null);

    public static DiskItemFilterValidation Invalid(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new DiskItemFilterValidation(false, message);
    }
}
