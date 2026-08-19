using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.Converters;

public sealed class SharedAwareMeasurementModeConverter : IValueConverter
{
    public static SharedAwareMeasurementModeConverter Instance { get; } = new();

    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        value is StorageMeasurementMode.SharedAwareAllocated;

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        BindingOperations.DoNothing;
}
