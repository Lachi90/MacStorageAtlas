namespace MacStorageAtlas.Core.Filtering;

public abstract record DateCriterion
{
    public abstract DateTimeOffset Resolve(DateTimeOffset referenceTime);

    public abstract DiskItemFilterValidation Validate();
}
