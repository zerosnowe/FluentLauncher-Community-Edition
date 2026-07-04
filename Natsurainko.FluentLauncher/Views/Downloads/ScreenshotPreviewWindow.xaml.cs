using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Natsurainko.FluentLauncher.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Foundation;
using WinUIEx;

namespace Natsurainko.FluentLauncher.Views.Downloads;

public sealed partial class ScreenshotPreviewWindow : WindowEx
{
    private const double MinScale = 0.2;
    private const double MaxScale = 8.0;
    private const double ZoomStep = 1.12;

    private static readonly HashSet<ScreenshotPreviewWindow> OpenWindows = [];

    private bool _isDragging;
    private Point _lastPointerPosition;

    public ScreenshotPreviewWindow(string imageUrl)
    {
        InitializeComponent();
        ConfigureWindow(GetImageName(imageUrl));

        PreviewImage.Source = new BitmapImage(new Uri(imageUrl));

        OpenWindows.Add(this);
        Closed += (_, _) => OpenWindows.Remove(this);
    }

    private void ConfigureWindow(string imageName)
    {
        this.ConfigureTitleBarTheme();
        this.ConfigureElementTheme();

        Title = imageName;
        AppWindow.Title = imageName;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;

        (Width, Height) = (980, 680);
        this.CenterOnScreen();
    }

    private void Viewport_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Viewport);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        _isDragging = true;
        _lastPointerPosition = point.Position;
        Viewport.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Viewport_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
            return;

        var position = e.GetCurrentPoint(Viewport).Position;
        ImageTransform.TranslateX += position.X - _lastPointerPosition.X;
        ImageTransform.TranslateY += position.Y - _lastPointerPosition.Y;
        _lastPointerPosition = position;
        e.Handled = true;
    }

    private void Viewport_PointerReleased(object sender, PointerRoutedEventArgs e) => StopDragging(e);

    private void Viewport_PointerCanceled(object sender, PointerRoutedEventArgs e) => StopDragging(e);

    private void Viewport_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Viewport);
        double oldScale = ImageTransform.ScaleX == 0 ? 1.0 : ImageTransform.ScaleX;
        double factor = point.Properties.MouseWheelDelta > 0 ? ZoomStep : 1 / ZoomStep;
        double newScale = Math.Clamp(oldScale * factor, MinScale, MaxScale);

        if (Math.Abs(newScale - oldScale) < 0.001)
            return;

        var position = point.Position;
        double contentX = (position.X - ImageTransform.TranslateX) / oldScale;
        double contentY = (position.Y - ImageTransform.TranslateY) / oldScale;

        ImageTransform.ScaleX = newScale;
        ImageTransform.ScaleY = newScale;
        ImageTransform.TranslateX = position.X - contentX * newScale;
        ImageTransform.TranslateY = position.Y - contentY * newScale;

        e.Handled = true;
    }

    private void Viewport_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        ImageTransform.ScaleX = 1;
        ImageTransform.ScaleY = 1;
        ImageTransform.TranslateX = 0;
        ImageTransform.TranslateY = 0;
    }

    private void StopDragging(PointerRoutedEventArgs e)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        Viewport.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private static string GetImageName(string imageUrl)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
            return "Screenshot";

        string fileName = Path.GetFileName(uri.LocalPath);
        return string.IsNullOrWhiteSpace(fileName)
            ? "Screenshot"
            : Uri.UnescapeDataString(fileName);
    }
}
