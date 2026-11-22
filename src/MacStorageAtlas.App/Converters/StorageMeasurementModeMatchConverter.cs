using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.Converters;

public sealed class StorageMeasurementModeMatchConverter : IValueConverter
{
    public static StorageMeasurementModeMatchConverter Instance { get; } = new();

    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        value is StorageMeasurementMode mode
        && parameter is StorageMeasurementMode expected
        && mode == expected;

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        BindingOperations.DoNothing;
}
