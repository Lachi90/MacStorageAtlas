using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using MacStorageAtlas.Rendering;

namespace MacStorageAtlas.App.Controls;

/// <summary>
/// Renders a precomputed treemap without creating a control for each rectangle.
/// </summary>
public sealed class TreemapControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<TreemapRect>?> RectanglesProperty =
        AvaloniaProperty.Register<TreemapControl, IReadOnlyList<TreemapRect>?>(nameof(Rectangles));

    public static readonly StyledProperty<TreemapRect?> SelectedRectangleProperty =
        AvaloniaProperty.Register<TreemapControl, TreemapRect?>(
            nameof(SelectedRectangle),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly DirectProperty<TreemapControl, TreemapRect?> HoveredRectangleProperty =
        AvaloniaProperty.RegisterDirect<TreemapControl, TreemapRect?>(
            nameof(HoveredRectangle),
            control => control.HoveredRectangle);

    private readonly Dictionary<string, IBrush> _brushes = new(StringComparer.Ordinal);
    private INotifyCollectionChanged? _observableRectangles;
    private TreemapRect? _hoveredRectangle;
    private IPen? _borderPen;
    private IPen? _hoverPen;
    private IPen? _selectionPen;

    static TreemapControl()
    {
        AffectsRender<TreemapControl>(RectanglesProperty, SelectedRectangleProperty);
    }

    public TreemapControl()
    {
        // Rebuild theme-derived pens and the cached block palette whenever the
        // effective light/dark variant changes so the treemap matches the app.
        ActualThemeVariantChanged += (_, _) =>
        {
            _borderPen = null;
            _hoverPen = null;
            _selectionPen = null;
            _brushes.Clear();
            InvalidateVisual();
        };

        // The layout is precomputed in an abstract coordinate space and scaled
        // to fit; repaint whenever the control is resized so it stays filled.
        SizeChanged += (_, _) => InvalidateVisual();
    }

    public IReadOnlyList<TreemapRect>? Rectangles
    {
        get => GetValue(RectanglesProperty);
        set => SetValue(RectanglesProperty, value);
    }

    public TreemapRect? SelectedRectangle
    {
        get => GetValue(SelectedRectangleProperty);
        set => SetValue(SelectedRectangleProperty, value);
    }

    public TreemapRect? HoveredRectangle => _hoveredRectangle;

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Rectangles is not { Count: > 0 } rectangles)
        {
            return;
        }

        EnsurePens();

        var (scaleX, scaleY) = GetScale(rectangles);

        foreach (var rectangle in rectangles)
        {
            if (!IsRenderable(rectangle))
            {
                continue;
            }

            var bounds = ToAvaloniaRect(rectangle, scaleX, scaleY);
            context.DrawRectangle(GetBrush(rectangle), _borderPen, bounds);

            if (rectangle.Equals(SelectedRectangle))
            {
                context.DrawRectangle(null, _selectionPen, bounds.Deflate(1.5));
            }
            else if (rectangle.Equals(HoveredRectangle))
            {
                context.DrawRectangle(null, _hoverPen, bounds.Deflate(1));
            }
        }
    }

    private void EnsurePens()
    {
        if (_borderPen is not null)
        {
            return;
        }

        var stroke = ResolveColor("TreemapStrokeColor", Color.FromArgb(120, 0, 0, 0));
        var highlight = ResolveColor("TreemapHighlightColor", Colors.White);

        _borderPen = new Pen(new SolidColorBrush(stroke), 1);
        _hoverPen = new Pen(new SolidColorBrush(highlight), 2);
        _selectionPen = new Pen(new SolidColorBrush(highlight), 3);
    }

    private Color ResolveColor(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is Color color
            ? color
            : fallback;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RectanglesProperty)
        {
            ObserveCollectionChanges();
            _brushes.Clear();
            SetHoveredRectangle(null);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        SetHoveredRectangle(HitTest(e.GetPosition(this)));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHoveredRectangle(null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
        {
            SelectedRectangle = HitTest(e.GetPosition(this));
            e.Handled = true;
        }
    }

    private TreemapRect? HitTest(Point point)
    {
        if (Rectangles is not { Count: > 0 } rectangles)
        {
            return null;
        }

        var (scaleX, scaleY) = GetScale(rectangles);

        for (var index = rectangles.Count - 1; index >= 0; index--)
        {
            var rectangle = rectangles[index];
            if (IsRenderable(rectangle) && ToAvaloniaRect(rectangle, scaleX, scaleY).Contains(point))
            {
                return rectangle;
            }
        }

        return null;
    }

    private void ObserveCollectionChanges()
    {
        if (_observableRectangles is not null)
        {
            _observableRectangles.CollectionChanged -= OnRectanglesCollectionChanged;
        }

        _observableRectangles = Rectangles as INotifyCollectionChanged;

        if (_observableRectangles is not null)
        {
            _observableRectangles.CollectionChanged += OnRectanglesCollectionChanged;
        }
    }

    private void OnRectanglesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _brushes.Clear();
        SetHoveredRectangle(null);
        InvalidateVisual();
    }

    private void SetHoveredRectangle(TreemapRect? value)
    {
        if (_hoveredRectangle.Equals(value))
        {
            return;
        }

        var oldValue = _hoveredRectangle;
        _hoveredRectangle = value;
        RaisePropertyChanged(HoveredRectangleProperty, oldValue, value);
        InvalidateVisual();
    }

    private IBrush GetBrush(TreemapRect rectangle)
    {
        var key = rectangle.Item.Item.Path;
        if (_brushes.TryGetValue(key, out var brush))
        {
            return brush;
        }

        // A stable path hash keeps colors consistent across layout recalculations.
        uint hash = 2166136261;
        foreach (var character in key)
        {
            hash = (hash ^ character) * 16777619;
        }

        var hue = hash % 360;
        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        var saturation = isDark ? 0.52 : 0.62;
        var value = isDark ? 0.62 : 0.82;
        brush = new SolidColorBrush(ColorFromHsv(hue, saturation, value));
        _brushes[key] = brush;
        return brush;
    }

    private static Color ColorFromHsv(double hue, double saturation, double value)
    {
        var chroma = value * saturation;
        var hueSection = hue / 60;
        var secondary = chroma * (1 - Math.Abs((hueSection % 2) - 1));
        var (red, green, blue) = hueSection switch
        {
            < 1 => (chroma, secondary, 0d),
            < 2 => (secondary, chroma, 0d),
            < 3 => (0d, chroma, secondary),
            < 4 => (0d, secondary, chroma),
            < 5 => (secondary, 0d, chroma),
            _ => (chroma, 0d, secondary),
        };
        var match = value - chroma;

        return Color.FromRgb(
            (byte)Math.Round((red + match) * 255),
            (byte)Math.Round((green + match) * 255),
            (byte)Math.Round((blue + match) * 255));
    }

    private static bool IsRenderable(TreemapRect rectangle) =>
        double.IsFinite(rectangle.X)
        && double.IsFinite(rectangle.Y)
        && double.IsFinite(rectangle.Width)
        && double.IsFinite(rectangle.Height)
        && rectangle.Width > 0
        && rectangle.Height > 0;

    /// <summary>
    /// The layout is produced in an abstract coordinate space (see
    /// <c>MainWindowViewModel</c>). We scale it to the control's actual size so
    /// the treemap always fills its container and adapts when resized.
    /// </summary>
    private (double ScaleX, double ScaleY) GetScale(IReadOnlyList<TreemapRect> rectangles)
    {
        double maxRight = 0;
        double maxBottom = 0;
        foreach (var rectangle in rectangles)
        {
            if (!IsRenderable(rectangle))
            {
                continue;
            }

            maxRight = Math.Max(maxRight, rectangle.X + rectangle.Width);
            maxBottom = Math.Max(maxBottom, rectangle.Y + rectangle.Height);
        }

        var width = Bounds.Width;
        var height = Bounds.Height;
        var scaleX = maxRight > 0 && width > 0 ? width / maxRight : 1;
        var scaleY = maxBottom > 0 && height > 0 ? height / maxBottom : 1;
        return (scaleX, scaleY);
    }

    private static Rect ToAvaloniaRect(TreemapRect rectangle, double scaleX, double scaleY) =>
        new(
            rectangle.X * scaleX,
            rectangle.Y * scaleY,
            rectangle.Width * scaleX,
            rectangle.Height * scaleY);
}
