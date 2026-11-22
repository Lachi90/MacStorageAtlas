using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.App.Converters;

public sealed class CloudAvailabilityConverter : IValueConverter
{
    private readonly Func<CloudAvailability, object> _convert;

    private CloudAvailabilityConverter(Func<CloudAvailability, object> convert)
    {
        _convert = convert;
    }

    public static CloudAvailabilityConverter IsCloudOnly { get; } =
        new(availability => availability == CloudAvailability.CloudOnly);

    public static CloudAvailabilityConverter IsPartlyLocal { get; } =
        new(availability => availability == CloudAvailability.PartlyLocal);

    public static CloudAvailabilityConverter Label { get; } =
        new(availability => Describe(availability));

    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        _convert(value is CloudAvailability availability ? availability : CloudAvailability.Unknown);

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        BindingOperations.DoNothing;

    public static string Describe(CloudAvailability availability) =>
        availability switch
        {
            CloudAvailability.Local => "Local",
            CloudAvailability.CloudOnly => "Only in the cloud",
            CloudAvailability.PartlyLocal => "Partly local",
            _ => "Unknown"
        };
}
