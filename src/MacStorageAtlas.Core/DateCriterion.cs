namespace MacStorageAtlas.Core;

public abstract record DateCriterion
{
    public abstract DateTimeOffset Resolve(DateTimeOffset referenceTime);

    public abstract DiskItemFilterValidation Validate();
}
