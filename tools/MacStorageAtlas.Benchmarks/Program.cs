namespace MacStorageAtlas.Benchmarks;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var cli = new BenchmarkCli(Console.Out, Console.Error);
        return await cli.RunAsync(args);
    }
}
