namespace MacStorageAtlas.Core;

public sealed record AbsoluteDateCriterion(DateTimeOffset Instant) : DateCriterion
{
    public override DateTimeOffset Resolve(DateTimeOffset referenceTime) => Instant;

    public override DiskItemFilterValidation Validate() => DiskItemFilterValidation.Valid;
}
