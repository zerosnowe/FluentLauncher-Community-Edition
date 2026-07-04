using CommunityToolkit.WinUI.Controls;
using FluentLauncher.Infra.UI.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Natsurainko.FluentLauncher.Utils;
using Natsurainko.FluentLauncher.ViewModels.Downloads;
using Nrk.FluentCore.Resources;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Natsurainko.FluentLauncher.Views.Downloads;

internal record ResourceAuthor(string Name, string WebLink);

public sealed partial class ResourcePage : Page, IBreadcrumbBarAware
{
    string IBreadcrumbBarAware.Route => "Resource";

    ResourceViewModel VM => (ResourceViewModel)DataContext;
    ResourceViewModel? _subscribedVM;

    public MarkdownConfig MarkdownConfig { get; set; } = new MarkdownConfig();

    public ResourcePage()
    {
        this.InitializeComponent();

        Loaded += ResourcePage_Loaded;
        Unloaded += ResourcePage_Unloaded;
    }

    private void ResourcePage_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ResourceViewModel viewModel)
            return;

        if (_subscribedVM == viewModel)
            return;

        if (_subscribedVM != null)
            _subscribedVM.PropertyChanged -= VM_PropertyChanged;

        _subscribedVM = viewModel;
        _subscribedVM.PropertyChanged += VM_PropertyChanged;
        RenderDependencyResources();
    }

    private void ResourcePage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedVM != null)
            _subscribedVM.PropertyChanged -= VM_PropertyChanged;

        _subscribedVM = null;
    }

    private void VM_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ResourceViewModel.DependencyResources))
            DispatcherQueue.TryEnqueue(RenderDependencyResources);
    }

    private void DependenciesPanel_Loaded(object sender, RoutedEventArgs e) => RenderDependencyResources();

    private void RenderDependencyResources()
    {
        if (DependencyResourcesPanel == null)
            return;

        DependencyResourcesPanel.Children.Clear();

        if (DataContext is not ResourceViewModel viewModel || viewModel.DependencyResources == null)
            return;

        foreach (var resource in viewModel.DependencyResources)
            DependencyResourcesPanel.Children.Add(CreateDependencyResourceCard(resource));
    }

    private SettingsCard CreateDependencyResourceCard(object resource)
    {
        var card = new SettingsCard
        {
            Padding = new Thickness(16, 8, 16, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsClickEnabled = true,
            Command = VM.ResourceItemInvokeCommand,
            CommandParameter = resource,
            Header = CreateDependencyResourceHeader(resource)
        };

        return card;
    }

    private static Grid CreateDependencyResourceHeader(object resource)
    {
        var (name, summary, categories, iconUrl, supportInfo) = GetDependencyResourceInfo(resource);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconBorder = new Border
        {
            Width = 48,
            Height = 48,
            VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = (Brush)Application.Current.Resources["IconBorder"],
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4)
        };

        if (Uri.TryCreate(iconUrl, UriKind.Absolute, out var uri))
            iconBorder.Background = new ImageBrush { ImageSource = new BitmapImage(uri) };

        root.Children.Add(iconBorder);

        var contentPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 2
        };
        Grid.SetColumn(contentPanel, 2);

        contentPanel.Children.Add(new TextBlock
        {
            FontSize = 16,
            Style = (Style)Application.Current.Resources["BaseTextBlockStyle"],
            Text = name,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        });

        var summaryGrid = new Grid();
        summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var categoryPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };

        foreach (string category in categories.Take(3))
            categoryPanel.Children.Add(CreateCategoryChip(category));

        summaryGrid.Children.Add(categoryPanel);

        var summaryText = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["ApplicationSecondaryForegroundThemeBrush"],
            MaxLines = 1,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Text = summary,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        };
        Grid.SetColumn(summaryText, 2);
        summaryGrid.Children.Add(summaryText);

        contentPanel.Children.Add(summaryGrid);

        var supportGrid = new Grid { Margin = new Thickness(0.5, 0, 0, 0) };
        supportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        supportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        supportGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        supportGrid.Children.Add(new FontIcon
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 10,
            Foreground = (Brush)Application.Current.Resources["ApplicationSecondaryForegroundThemeBrush"],
            Glyph = "\uE73A"
        });

        var supportText = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["ApplicationSecondaryForegroundThemeBrush"],
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Text = supportInfo
        };
        Grid.SetColumn(supportText, 2);
        supportGrid.Children.Add(supportText);

        contentPanel.Children.Add(supportGrid);
        root.Children.Add(contentPanel);

        return root;
    }

    private static Border CreateCategoryChip(string category)
    {
        return new Border
        {
            Padding = new Thickness(5, 0, 5, 0.5),
            CornerRadius = new CornerRadius(2.5),
            Background = CreateCategoryChipBackground(),
            Child = new TextBlock
            {
                Margin = new Thickness(0, -1, 0, 0),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Text = GetLocalizedCategory(category)
            }
        };
    }

    private static SolidColorBrush CreateCategoryChipBackground()
    {
        var brush = new SolidColorBrush(Microsoft.UI.Colors.Transparent) { Opacity = 0.25 };

        if (Application.Current.Resources.TryGetValue("SystemAccentColor", out object colorResource)
            && colorResource is Windows.UI.Color color)
            brush.Color = color;

        return brush;
    }

    private static (string Name, string Summary, string[] Categories, string? IconUrl, string SupportInfo) GetDependencyResourceInfo(object resource)
    {
        string supportInfo = GetResourceSupportInfo(resource);

        return resource switch
        {
            CurseForgeResource curseForgeResource => (
                curseForgeResource.Name,
                curseForgeResource.Summary,
                [.. curseForgeResource.Categories],
                curseForgeResource.IconUrl,
                supportInfo),
            ModrinthResource modrinthResource => (
                modrinthResource.Name,
                modrinthResource.Summary,
                [.. modrinthResource.Categories],
                modrinthResource.IconUrl,
                supportInfo),
            _ => (string.Empty, string.Empty, [], null, string.Empty)
        };
    }

    private static string GetResourceSupportInfo(object resource)
    {
        if (Application.Current.Resources["ResourceSupportInfoConverter"] is IValueConverter converter)
            return converter.Convert(resource, typeof(string), null, string.Empty)?.ToString() ?? string.Empty;

        return string.Empty;
    }

    private static string GetLocalizedCategory(string category)
    {
        if (Application.Current.Resources["ResourceLocalizedCategoriesConverter"] is IValueConverter converter)
            return converter.Convert(category, typeof(string), null, string.Empty)?.ToString() ?? category;

        return category;
    }

    private void FilesItemsView_SelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args) => VM.SelectedFile = sender.SelectedItem;

    private void FilesItemsView_Unloaded(object sender, RoutedEventArgs e) => VM.SelectedFile = null;

    private void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string imageUrl } && Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
            new ScreenshotPreviewWindow(imageUrl).Activate();
    }

    private void DescriptionMarkdown_Loaded(object sender, RoutedEventArgs e) => DescriptionMarkdown.Text = VM.DescriptionContent;

    private async void DescriptionWebView2_Loaded(object sender, RoutedEventArgs e)
    {
        WebView2 webView2 = DescriptionWebView2;
        await webView2.EnsureCoreWebView2Async();

        webView2.CoreWebView2.Profile.PreferredColorScheme = webView2.ActualTheme == ElementTheme.Dark
            ? CoreWebView2PreferredColorScheme.Dark
            : CoreWebView2PreferredColorScheme.Light;

        string body = $"<meta name=\"color-scheme\" content=\"{(webView2.ActualTheme == ElementTheme.Dark ? "dark light" : "light dark")}\">\r\n"
            + "<style>img{width:auto;height:auto;max-width:100%;max-height:100%;}</style>\r\n"
            + $"<div id='container'>{VM.DescriptionContent}</div>";

        webView2.MinHeight = VM.DescriptionContent.Length / (webView2.ActualWidth / 8) * 14;
        webView2.NavigateToString(body);

        await Task.Delay(2000);

        var script = "eval(document.getElementById('container').getBoundingClientRect().height.toString());";
        var heightString = await webView2.ExecuteScriptAsync(script);

        if (double.TryParse(heightString, out double height))
            webView2.MinHeight = height + 30;
    }

    internal static string GetPreferredFolderOptionText(string folder, int index)
    {
        if (folder == null) return string.Empty;

        return index switch
        {
            0 => LocalizedStrings.Downloads_ResourcePage__M2.Replace("${folder}", folder),
            1 => LocalizedStrings.Downloads_ResourcePage__M3.Replace("${folder}", folder),
            _ => throw new NotImplementedException()
        };
    }
}
