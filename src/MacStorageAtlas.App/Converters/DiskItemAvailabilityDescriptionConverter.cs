using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.App.Converters;

public sealed class DiskItemAvailabilityDescriptionConverter : IValueConverter
{
    public static DiskItemAvailabilityDescriptionConverter Instance { get; } = new();

    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        value is DiskItem item
            ? CloudAvailabilityText.Describe(item, measurementMode: null)
            : null;

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        BindingOperations.DoNothing;
}
