using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace MacStorageAtlas.App.Converters;

public sealed class DisplayNameConverter : IValueConverter
{
    public const string EmptyNamePlaceholder = "⟨empty name⟩";

    public static DisplayNameConverter Instance { get; } = new();

    public static string Display(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return EmptyNamePlaceholder;
        }

        if (!name.All(IsInvisible))
        {
            return name;
        }

        var codePoints = string.Join(
            " ",
            name.EnumerateRunes().Select(rune => $"U+{rune.Value:X4}"));
        return $"⟨blank name: {codePoints}⟩";
    }

    public object Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        Display(value as string);

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture) =>
        BindingOperations.DoNothing;

    private static bool IsInvisible(char character) =>
        char.IsWhiteSpace(character)
        || char.GetUnicodeCategory(character) is UnicodeCategory.Format
            or UnicodeCategory.Control
            or UnicodeCategory.SpaceSeparator;
}
