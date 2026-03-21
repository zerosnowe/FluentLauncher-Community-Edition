using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Natsurainko.FluentLauncher.Utils.Extensions;
using System;
using System.Runtime.InteropServices;
using WinRT.Interop;
using WinUIEx;

namespace Natsurainko.FluentLauncher.Views;

public sealed partial class QuickLookWindow : WindowEx
{
    public string CurrentSiteName { get; private set; } = "Minecraft Wiki";

    private bool _isTopMost;

    public QuickLookWindow()
    {
        InitializeComponent();
        ConfigureWindow();
    }

    private void ConfigureWindow()
    {
        this.ConfigureTitleBarTheme();
        this.ConfigureElementTheme();

        AppWindow.Title = "QuickLook";
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;

        (Width, Height) = (980, 680);
        this.CenterOnScreen();

        // Make the top row the drag region; keep the switcher clickable.
        this.SetTitleBar(TitleBarRoot);
    }

    private async void WebView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            WebView2 webView2 = WebView;
            await webView2.EnsureCoreWebView2Async();

            webView2.CoreWebView2.Profile.PreferredColorScheme =
                webView2.ActualTheme == ElementTheme.Dark
                    ? CoreWebView2PreferredColorScheme.Dark
                    : CoreWebView2PreferredColorScheme.Light;

            // Prevent spawning a new WebView2 window (target=_blank etc); open in-place.
            webView2.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                if (!string.IsNullOrWhiteSpace(args.Uri))
                    webView2.CoreWebView2.Navigate(args.Uri);
            };

            webView2.CoreWebView2.HistoryChanged += (_, _) =>
                DispatcherQueue.TryEnqueue(UpdateBackButtonState);
        }
        catch
        {
            // Ignore WebView2 init failures (e.g., runtime missing).
        }

        UpdateBackButtonState();
    }

    private void Navigate(string url, string name)
    {
        CurrentSiteName = name;
        Bindings.Update();

        try
        {
            WebView.Source = new Uri(url);
        }
        catch
        {
            // Ignore invalid URLs.
        }
    }

    private void MinecraftWiki_Click(object sender, RoutedEventArgs e)
        => Navigate("https://zh.minecraft.wiki/", "Minecraft Wiki");

    private void McMod_Click(object sender, RoutedEventArgs e)
        => Navigate("https://www.mcmod.cn/", "MOD百科");

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CanGoBack)
            WebView.GoBack();

        UpdateBackButtonState();
    }

    private void TopMostToggle_Checked(object sender, RoutedEventArgs e)
    {
        SetTopMost(true);
        TopMostIcon.Glyph = "\uE840"; // Pinned
    }

    private void TopMostToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        SetTopMost(false);
        TopMostIcon.Glyph = "\uE718"; // Pin
    }

    private void SetTopMost(bool topMost)
    {
        _isTopMost = topMost;
        IntPtr hwnd = WindowNative.GetWindowHandle(this);

        _ = SetWindowPos(
            hwnd,
            topMost ? HwndTopMost : HwndNoTopMost,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    private void UpdateBackButtonState()
    {
        if (BackButton is null)
            return;

        BackButton.IsEnabled = WebView.CanGoBack;
    }

    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);
}
