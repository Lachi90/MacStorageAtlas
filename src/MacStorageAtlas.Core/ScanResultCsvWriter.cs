using System.Buffers;

namespace MacStorageAtlas.Core;

public static class ScanResultCsvWriter
{
    private const string FieldSeparator = ",";
    private const string LineSeparator = "\r\n";
    private static readonly SearchValues<char> QuoteTriggers =
        SearchValues.Create([',', '"', '\r', '\n']);
    private static readonly SearchValues<char> FormulaTriggers =
        SearchValues.Create(['=', '+', '-', '@', '\t', '\r']);

    public static IReadOnlyList<string> Headers { get; } =
    [
        "Path",
        "Name",
        "Kind",
        "Depth",
        "MeasurementMode",
        "MeasuredSizeBytes",
        "CountedSizeBytes",
        "SharedSizeBytes",
        "IsSharedStorage",
        "Extension",
        "Category",
        "CreatedUtc",
        "ModifiedUtc",
        "LastAccessedUtc"
    ];

    public static async Task WriteAsync(
        ScanExportRequest request,
        TextWriter writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(writer);

        for (var index = 0; index < Headers.Count; index++)
        {
            if (index > 0)
            {
                await writer.WriteAsync(FieldSeparator).ConfigureAwait(false);
            }

            await writer.WriteAsync(Headers[index]).ConfigureAwait(false);
        }

        await writer.WriteAsync(LineSeparator).ConfigureAwait(false);

        foreach (var row in request.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await WriteFieldAsync(writer, Text(row.Path), first: true).ConfigureAwait(false);
            await WriteFieldAsync(writer, Text(row.Name)).ConfigureAwait(false);
            await WriteFieldAsync(writer, Quote(row.Kind.ToString())).ConfigureAwait(false);
            await WriteFieldAsync(writer, ScanExportValues.Number(row.Depth))
                .ConfigureAwait(false);
            await WriteFieldAsync(writer, Quote(row.MeasurementMode.ToString()))
                .ConfigureAwait(false);
            await WriteFieldAsync(writer, ScanExportValues.Number(row.MeasuredSizeBytes))
                .ConfigureAwait(false);
            await WriteFieldAsync(writer, ScanExportValues.Number(row.CountedSizeBytes))
                .ConfigureAwait(false);
            await WriteFieldAsync(writer, ScanExportValues.Number(row.SharedSizeBytes))
                .ConfigureAwait(false);
            await WriteFieldAsync(writer, ScanExportValues.Boolean(row.IsSharedStorage))
                .ConfigureAwait(false);
            await WriteFieldAsync(writer, Text(row.Extension)).ConfigureAwait(false);
            await WriteFieldAsync(writer, Quote(ScanExportValues.Category(row.Category)))
                .ConfigureAwait(false);
            await WriteFieldAsync(writer, ScanExportValues.Timestamp(row.CreatedUtc))
                .ConfigureAwait(false);
            await WriteFieldAsync(writer, ScanExportValues.Timestamp(row.ModifiedUtc))
                .ConfigureAwait(false);
            await WriteFieldAsync(writer, ScanExportValues.Timestamp(row.LastAccessedUtc))
                .ConfigureAwait(false);
            await writer.WriteAsync(LineSeparator).ConfigureAwait(false);
        }
    }

    private static async Task WriteFieldAsync(
        TextWriter writer,
        string value,
        bool first = false)
    {
        if (!first)
        {
            await writer.WriteAsync(FieldSeparator).ConfigureAwait(false);
        }

        await writer.WriteAsync(value).ConfigureAwait(false);
    }

    internal static string Text(string value) => Quote(NeutralizeFormula(value));

    private static string NeutralizeFormula(string value) =>
        value.Length > 0 && FormulaTriggers.Contains(value[0])
            ? string.Concat("'", value)
            : value;

    private static string Quote(string value) =>
        value.AsSpan().ContainsAny(QuoteTriggers)
            ? string.Concat("\"", value.Replace("\"", "\"\"", StringComparison.Ordinal), "\"")
            : value;
}
