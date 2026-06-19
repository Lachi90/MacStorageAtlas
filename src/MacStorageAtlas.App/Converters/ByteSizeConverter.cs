using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Converters;

/// <summary>
/// Formats a byte count (<see cref="long"/>) into a human-readable size string
/// using <see cref="FileSizeFormatter"/>.
/// </summary>
public sealed class ByteSizeConverter : IValueConverter
{
    public static ByteSizeConverter Instance { get; } = new();

    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        value is long bytes ? FileSizeFormatter.Format(bytes) : string.Empty;

    // Display-only converter: there is no meaningful way to turn a formatted
    // size string back into a byte count, so writing back is a no-op.
    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        BindingOperations.DoNothing;
}
