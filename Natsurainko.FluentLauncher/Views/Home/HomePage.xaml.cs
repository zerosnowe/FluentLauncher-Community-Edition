using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Globalization;
using Natsurainko.FluentLauncher.Services.Settings;
using Natsurainko.FluentLauncher.Utils;
using Natsurainko.FluentLauncher.ViewModels.Home;
using Nrk.FluentCore.Authentication;
using System;
using Windows.Foundation;
using Windows.UI;

namespace Natsurainko.FluentLauncher.Views.Home;

public sealed partial class HomePage : Page
{
    private readonly SettingsService _settingsService = App.GetService<SettingsService>();
    private DispatcherTimer? _newsCarouselTimer;

    HomeViewModel VM => (HomeViewModel)DataContext;

    public HomePage()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        var themeDictionaries = App.Current.Resources;

        if (_settingsService.UseHomeControlsMask)
        {
            Brush foregroundBrush = this.ActualTheme == ElementTheme.Light
                ? new SolidColorBrush(Color.FromArgb(255, 26, 26, 26))
                : new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            LaunchButton.Translation += new System.Numerics.Vector3(0, 0, 16);

            foreach (var border in new Border[] { InstanceSelectorArea, AccountSelectorArea, LaunchingInfoArea })
            {
                border.Translation += new System.Numerics.Vector3(0, 0, 16);
                border.Background = themeDictionaries["NavigationViewUnfoldedPaneBackground"] as AcrylicBrush;
                border.BorderThickness = new Thickness(1);
                border.BorderBrush = themeDictionaries["ButtonBorderBrushPointerOver"] as Brush;
            }

            AccountSelectorButton.Foreground = foregroundBrush;

            this.ActualThemeChanged += (_, e) =>
            {
                var themeDictionaries = App.Current.Resources;

                Brush foregroundBrush = this.ActualTheme == ElementTheme.Light
                    ? new SolidColorBrush(Color.FromArgb(255, 26, 26, 26))
                    : new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

                foreach (var border in new Border[] { InstanceSelectorArea, AccountSelectorArea, LaunchingInfoArea })
                {
                    border.Background = themeDictionaries["NavigationViewUnfoldedPaneBackground"] as AcrylicBrush;
                    border.BorderThickness = new Thickness(1);
                    border.BorderBrush = themeDictionaries["ButtonBorderBrushPointerOver"] as Brush;
                }

                AccountSelectorButton.Foreground = foregroundBrush;
            };
        }

        if (_settingsService.HomeLaunchButtonSize == 1)
        {
            LaunchButtonIcon.FontSize = 22;
            LaunchButton.Width = 56;
            LaunchButton.Height = 56;
            LaunchButton.CornerRadius = new CornerRadius(28);
        }

        InstanceSelectorGrid.TranslationTransition = new Vector3Transition()
        {
            Duration = TimeSpan.FromMilliseconds(500)
        };
        LaunchingInfoGrid.TranslationTransition = new Vector3Transition()
        {
            Duration = TimeSpan.FromMilliseconds(500)
        };

        InstanceSelectorGrid.OpacityTransition = new ScalarTransition()
        {
            Duration = TimeSpan.FromMilliseconds(250)
        };
        LaunchingInfoGrid.OpacityTransition = new ScalarTransition()
        {
            Duration = TimeSpan.FromMilliseconds(250)
        };

        LaunchButton.Focus(FocusState.Programmatic);

        StartNewsCarousel();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        StopNewsCarousel();
        this.DataContext = null;

        InstancesListView.ItemsSource = null;
        AccountsListView.ItemsSource = null;
    }

    private void Flyout_Opened(object sender, object e) => InstancesListView.ScrollIntoView(VM.ActiveMinecraftInstance);

    private void HideAccountFlyoutHandler(object sender, RoutedEventArgs e) => accountSelectorFlyout.Hide();

    private void DropDownButton_Click(object sender, RoutedEventArgs e)
    {
        var transform = DropDownButton.TransformToVisual(Grid);
        var absolutePosition = transform.TransformPoint(new Point(0, 0));

        InstancesListView.MaxHeight = absolutePosition.Y - 50;

        if (this.ActualWidth > 550)
        {
            InstancesListView.MaxWidth = 400;
            InstancesListView.Width = double.NaN;
        }
        else
        {
            InstancesListView.MaxWidth = 430;
            InstancesListView.Width = 430;
        }
    }

    private void StartNewsCarousel()
    {
        if (_newsCarouselTimer is not null)
            return;

        _newsCarouselTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _newsCarouselTimer.Tick += (_, _) =>
        {
            int count = VM.NewsCarouselCount;
            if (count <= 1)
                return;

            int next = VM.CurrentNewsIndex + 1;
            if (next >= count)
                next = 0;

            VM.CurrentNewsIndex = next;
        };
        _newsCarouselTimer.Start();
    }

    private void StopNewsCarousel()
    {
        if (_newsCarouselTimer is null)
            return;

        _newsCarouselTimer.Stop();
        _newsCarouselTimer = null;
    }

    #region Converters Methods

    internal static string GetAccountTypeName(AccountType accountType)
    {
        string account = LocalizedStrings.Converters__Account;

        if (!ApplicationLanguages.PrimaryLanguageOverride.StartsWith("zh-"))
            account = " " + account;

        return accountType switch
        {
            AccountType.Microsoft => LocalizedStrings.Converters__Microsoft + account,
            AccountType.Yggdrasil => LocalizedStrings.Converters__Yggdrasil + account,
            _ => LocalizedStrings.Converters__Offline + account,
        };
    }

    internal static string TryGetYggdrasilServerName(Account account)
    {
        if (account is YggdrasilAccount yggdrasilAccount)
        {
            if (yggdrasilAccount.MetaData.TryGetValue("server_name", out var serverName))
                return serverName;
        }

        return string.Empty;
    }

    #endregion
}
