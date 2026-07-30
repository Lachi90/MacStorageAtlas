using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Converters;

public sealed class StorageMeasurementModeLabelConverter : IValueConverter
{
    public static StorageMeasurementModeLabelConverter Instance { get; } = new();

    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        value is StorageMeasurementMode mode
            ? Label(mode)
            : string.Empty;

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        BindingOperations.DoNothing;

    public static string Label(StorageMeasurementMode mode) =>
        mode switch
        {
            StorageMeasurementMode.Logical => "Logical size",
            StorageMeasurementMode.Allocated => "Allocated size per path",
            StorageMeasurementMode.SharedAwareAllocated =>
                "Shared-aware allocated size",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
}
