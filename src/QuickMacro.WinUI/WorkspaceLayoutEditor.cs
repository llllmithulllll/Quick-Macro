using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using QuickMacro.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace QuickMacro.WinUI;

/// <summary>Interactive percentage-based desktop layout editor for workspace windows.</summary>
internal sealed class WorkspaceLayoutEditor : Grid
{
    private readonly IList<WorkspaceItem> _items;
    private readonly Func<WorkspaceItem, string> _name;
    private readonly Canvas _canvas = new();
    private readonly Border _backdrop = new();
    private WorkspaceItem? _active;
    private Point _last;
    private int _edges;
    private double _screenLeft, _screenTop, _screenWidth, _screenHeight;

    private const int Left = 1, Top = 2, Right = 4, Bottom = 8;

    public WorkspaceLayoutEditor(IList<WorkspaceItem> items, Func<WorkspaceItem, string> name)
    {
        _items = items;
        _name = name;
        Height = 430;
        MinHeight = 280;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        _backdrop.BorderThickness = new Thickness(1);
        _backdrop.CornerRadius = new CornerRadius(12);
        Children.Add(_backdrop);
        Children.Add(_canvas);
        _canvas.SizeChanged += (_, _) => Render();
        ActualThemeChanged += (_, _) => Render();
        _canvas.PointerPressed += OnPointerPressed;
        _canvas.PointerMoved += OnPointerMoved;
        _canvas.PointerReleased += OnPointerReleased;
        _canvas.PointerCanceled += OnPointerReleased;
    }

    public void RefreshItems() => Render();

    private void Render()
    {
        if (_canvas.ActualWidth < 80 || _canvas.ActualHeight < 80) return;
        _canvas.Children.Clear();
        // Keep the desktop preview at a realistic monitor aspect ratio and center it
        // in the editor, instead of stretching it into a narrow/tall rectangle.
        var availableWidth = Math.Max(40, _canvas.ActualWidth - 44);
        var availableHeight = Math.Max(40, _canvas.ActualHeight - 36);
        _screenWidth = Math.Min(availableWidth, availableHeight * 16 / 9);
        _screenHeight = _screenWidth * 9 / 16;
        _screenLeft = (_canvas.ActualWidth - _screenWidth) / 2;
        _screenTop = (_canvas.ActualHeight - _screenHeight) / 2;
        var dark = ActualTheme == ElementTheme.Dark;
        _backdrop.Background = new SolidColorBrush(dark ? ColorHelper.FromArgb(255, 20, 29, 44) : ColorHelper.FromArgb(255, 235, 240, 247));
        _backdrop.BorderBrush = new SolidColorBrush(dark ? ColorHelper.FromArgb(255, 50, 66, 88) : ColorHelper.FromArgb(255, 202, 213, 228));

        var screen = new Border
        {
            Width = _screenWidth,
            Height = _screenHeight,
            Background = new SolidColorBrush(dark ? ColorHelper.FromArgb(255, 15, 25, 40) : ColorHelper.FromArgb(255, 248, 250, 253)),
            BorderBrush = new SolidColorBrush(dark ? ColorHelper.FromArgb(255, 93, 119, 151) : ColorHelper.FromArgb(255, 130, 151, 178)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(screen, _screenLeft);
        Canvas.SetTop(screen, _screenTop);
        _canvas.Children.Add(screen);

        for (var i = 1; i < 8; i++)
        {
            var x = _screenLeft + _screenWidth * i / 8;
            var y = _screenTop + _screenHeight * i / 8;
            AddLine(x, _screenTop, x, _screenTop + _screenHeight);
            AddLine(_screenLeft, y, _screenLeft + _screenWidth, y);
        }

        foreach (var item in _items)
        {
            var card = new Border
            {
                Width = Math.Max(26, _screenWidth * item.Width / 100),
                Height = Math.Max(24, _screenHeight * item.Height / 100),
                Background = item == _active
                    ? new SolidColorBrush(dark ? ColorHelper.FromArgb(245, 43, 105, 195) : ColorHelper.FromArgb(245, 68, 133, 237))
                    : new SolidColorBrush(dark ? ColorHelper.FromArgb(230, 48, 81, 124) : ColorHelper.FromArgb(230, 197, 218, 249)),
                BorderBrush = item == _active
                    ? new SolidColorBrush(ColorHelper.FromArgb(255, 168, 204, 255))
                    : new SolidColorBrush(dark ? ColorHelper.FromArgb(255, 113, 157, 213) : ColorHelper.FromArgb(255, 127, 165, 213)),
                BorderThickness = new Thickness(item == _active ? 2 : 1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 7, 10, 7),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = _name(item), FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = new SolidColorBrush(dark || item == _active ? Colors.White : ColorHelper.FromArgb(255, 30, 46, 68)) },
                        new TextBlock { Text = $"{item.Width}% × {item.Height}%", FontSize = 11, Opacity = .82, Foreground = new SolidColorBrush(dark || item == _active ? Colors.White : ColorHelper.FromArgb(255, 30, 46, 68)) }
                    }
                }
            };
            Canvas.SetLeft(card, _screenLeft + _screenWidth * item.X / 100);
            Canvas.SetTop(card, _screenTop + _screenHeight * item.Y / 100);
            _canvas.Children.Add(card);
        }
        // Keep resize targets visible for every app so users can grab any edge
        // immediately, without first clicking to select its preview card.
        foreach (var item in _items) AddResizeHandles(item);
    }

    private void AddLine(double x1, double y1, double x2, double y2)
    {
        var line = new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = new SolidColorBrush(ColorHelper.FromArgb(28, 159, 190, 232)),
            StrokeThickness = 1,
            IsHitTestVisible = false
        };
        _canvas.Children.Add(line);
    }

    private void AddResizeHandles(WorkspaceItem item)
    {
        var x = _screenLeft + _screenWidth * item.X / 100;
        var y = _screenTop + _screenHeight * item.Y / 100;
        var w = _screenWidth * item.Width / 100;
        var h = _screenHeight * item.Height / 100;
        foreach (var (px, py) in new[] { (x, y), (x + w / 2, y), (x + w, y), (x, y + h / 2), (x + w, y + h / 2), (x, y + h), (x + w / 2, y + h), (x + w, y + h) })
        {
            var dot = new Ellipse
            {
                Width = 8, Height = 8,
                Fill = new SolidColorBrush(Colors.White),
                Stroke = new SolidColorBrush(ColorHelper.FromArgb(255, 45, 112, 213)),
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, px - 4);
            Canvas.SetTop(dot, py - 4);
            _canvas.Children.Add(dot);
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_canvas).Position;
        _active = _items.LastOrDefault(item => Contains(item, point));
        if (_active is null) { Render(); return; }
        _edges = FindEdges(_active, point);
        _last = point;
        _canvas.CapturePointer(e.Pointer);
        e.Handled = true;
        Render();
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_active is null || !_canvas.PointerCaptures.Any(pointer => pointer.PointerId == e.Pointer.PointerId)) return;
        var point = e.GetCurrentPoint(_canvas).Position;
        var dx = (point.X - _last.X) * 100 / _screenWidth;
        var dy = (point.Y - _last.Y) * 100 / _screenHeight;
        _last = point;
        var item = _active;
        if (_edges == 0)
        {
            item.X = Clamp((int)Math.Round(item.X + dx), 0, 100 - item.Width);
            item.Y = Clamp((int)Math.Round(item.Y + dy), 0, 100 - item.Height);
            SnapMove(item);
        }
        else { Resize(item, (int)Math.Round(dx), (int)Math.Round(dy)); SnapResize(item); }
        Render();
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_canvas.PointerCaptures.Any(pointer => pointer.PointerId == e.Pointer.PointerId)) _canvas.ReleasePointerCapture(e.Pointer);
        _active = null;
        _edges = 0;
        Render();
    }

    private bool Contains(WorkspaceItem item, Point point)
    {
        var l = _screenLeft + _screenWidth * item.X / 100;
        var t = _screenTop + _screenHeight * item.Y / 100;
        return point.X >= l && point.X <= l + _screenWidth * item.Width / 100 && point.Y >= t && point.Y <= t + _screenHeight * item.Height / 100;
    }

    private int FindEdges(WorkspaceItem item, Point point)
    {
        var l = _screenLeft + _screenWidth * item.X / 100;
        var t = _screenTop + _screenHeight * item.Y / 100;
        var r = l + _screenWidth * item.Width / 100;
        var b = t + _screenHeight * item.Height / 100;
        var edges = 0;
        if (point.X - l < 12) edges |= Left;
        else if (r - point.X < 12) edges |= Right;
        if (point.Y - t < 12) edges |= Top;
        else if (b - point.Y < 12) edges |= Bottom;
        return edges;
    }

    private void Resize(WorkspaceItem item, int dx, int dy)
    {
        if ((_edges & Left) != 0) { item.X = Clamp(item.X + dx, 0, item.X + item.Width - 8); item.Width = Clamp(item.Width - dx, 8, 100 - item.X); }
        if ((_edges & Right) != 0) item.Width = Clamp(item.Width + dx, 8, 100 - item.X);
        if ((_edges & Top) != 0) { item.Y = Clamp(item.Y + dy, 0, item.Y + item.Height - 8); item.Height = Clamp(item.Height - dy, 8, 100 - item.Y); }
        if ((_edges & Bottom) != 0) item.Height = Clamp(item.Height + dy, 8, 100 - item.Y);
    }

    private void SnapMove(WorkspaceItem moving)
    {
        const int snap = 2;
        if (moving.X <= snap) moving.X = 0;
        if (moving.Y <= snap) moving.Y = 0;
        if (100 - moving.X - moving.Width <= snap) moving.X = 100 - moving.Width;
        if (100 - moving.Y - moving.Height <= snap) moving.Y = 100 - moving.Height;
        foreach (var other in _items.Where(item => item != moving))
        {
            if (Math.Abs(moving.X + moving.Width - other.X) <= snap) moving.X = other.X - moving.Width;
            if (Math.Abs(other.X + other.Width - moving.X) <= snap) moving.X = other.X + other.Width;
            if (Math.Abs(moving.Y + moving.Height - other.Y) <= snap) moving.Y = other.Y - moving.Height;
            if (Math.Abs(other.Y + other.Height - moving.Y) <= snap) moving.Y = other.Y + other.Height;
            if (Math.Abs(moving.X - other.X) <= snap) moving.X = other.X;
            if (Math.Abs(moving.Y - other.Y) <= snap) moving.Y = other.Y;
        }
        moving.X = Clamp(moving.X, 0, 100 - moving.Width);
        moving.Y = Clamp(moving.Y, 0, 100 - moving.Height);
    }

    private void SnapResize(WorkspaceItem moving)
    {
        const int snap = 2;
        var peers = _items.Where(item => item != moving).ToArray();
        if ((_edges & Left) != 0)
        {
            var fixedRight = moving.X + moving.Width;
            var edge = NearestEdge(moving.X, peers.SelectMany(item => new[] { item.X, item.X + item.Width }).Append(0), snap);
            if (edge >= 0 && edge < fixedRight - 8) { moving.X = edge; moving.Width = fixedRight - edge; }
        }
        if ((_edges & Right) != 0)
        {
            var edge = NearestEdge(moving.X + moving.Width, peers.SelectMany(item => new[] { item.X, item.X + item.Width }).Append(100), snap);
            if (edge > moving.X + 8 && edge <= 100) moving.Width = edge - moving.X;
        }
        if ((_edges & Top) != 0)
        {
            var fixedBottom = moving.Y + moving.Height;
            var edge = NearestEdge(moving.Y, peers.SelectMany(item => new[] { item.Y, item.Y + item.Height }).Append(0), snap);
            if (edge >= 0 && edge < fixedBottom - 8) { moving.Y = edge; moving.Height = fixedBottom - edge; }
        }
        if ((_edges & Bottom) != 0)
        {
            var edge = NearestEdge(moving.Y + moving.Height, peers.SelectMany(item => new[] { item.Y, item.Y + item.Height }).Append(100), snap);
            if (edge > moving.Y + 8 && edge <= 100) moving.Height = edge - moving.Y;
        }
    }

    private static int NearestEdge(int value, IEnumerable<int> edges, int threshold)
    {
        var matches = edges.Select(edge => (Edge: edge, Distance: Math.Abs(edge - value)))
            .Where(candidate => candidate.Distance <= threshold)
            .OrderBy(candidate => candidate.Distance).ToArray();
        return matches.Length == 0 ? -1 : matches[0].Edge;
    }

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(value, max));
}
