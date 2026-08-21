namespace MacStorageAtlas.Core.Access;

public interface IAppSandboxDetector
{
    bool IsSandboxed { get; }
}
