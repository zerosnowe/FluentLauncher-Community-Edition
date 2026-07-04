using CommunityToolkit.WinUI.Controls;
using FluentLauncher.Infra.UI.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Natsurainko.FluentLauncher.Utils;
using Natsurainko.FluentLauncher.Utils.Extensions;
using Natsurainko.FluentLauncher.ViewModels.Instances;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Windows.ApplicationModel.DataTransfer;

namespace Natsurainko.FluentLauncher.Views.Instances;

public sealed partial class ResourceManagementPage : Page, IBreadcrumbBarAware
{
    public string Route => "Resource";

    ResourceManagementViewModel VM => (ResourceManagementViewModel)DataContext;
    ResourceManagementViewModel? _subscribedVM;

    public ResourceManagementPage()
    {
        InitializeComponent();
        Loaded += ResourceManagementPage_Loaded;
        Unloaded += ResourceManagementPage_Unloaded;
    }

    private void ResourceManagementPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ResourceManagementViewModel viewModel)
            return;

        if (_subscribedVM != null)
        {
            _subscribedVM.PropertyChanged -= VM_PropertyChanged;
            _subscribedVM.Items.CollectionChanged -= Items_CollectionChanged;
        }

        _subscribedVM = viewModel;
        _subscribedVM.PropertyChanged += VM_PropertyChanged;
        _subscribedVM.Items.CollectionChanged += Items_CollectionChanged;
        Render();
    }

    private void ResourceManagementPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedVM != null)
        {
            _subscribedVM.PropertyChanged -= VM_PropertyChanged;
            _subscribedVM.Items.CollectionChanged -= Items_CollectionChanged;
        }

        _subscribedVM = null;
    }

    private void VM_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ResourceManagementViewModel.Title)
            or nameof(ResourceManagementViewModel.FolderTitle)
            or nameof(ResourceManagementViewModel.FolderPath)
            or nameof(ResourceManagementViewModel.EmptyText)
            or nameof(ResourceManagementViewModel.ShowDownload))
            DispatcherQueue.TryEnqueue(Render);
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => DispatcherQueue.TryEnqueue(Render);

    private void Render()
    {
        if (DataContext is not ResourceManagementViewModel viewModel)
            return;

        RootPanel.Children.Clear();
        RootPanel.Children.Add(new TextBlock
        {
            Style = TryGetResource<Style>("SettingsTitleSectionHeaderTextBlockStyle"),
            Text = viewModel.Title
        });

        RootPanel.Children.Add(new SettingsCard
        {
            Header = viewModel.FolderTitle,
            Description = viewModel.FolderPath,
            HeaderIcon = new FontIcon { Glyph = "\uED43" },
            ActionIcon = new FontIcon { Glyph = "\uED43" },
            Command = viewModel.OpenFolderCommand,
            IsClickEnabled = true
        });

        if (viewModel.ShowDownload)
        {
            RootPanel.Children.Add(new SettingsCard
            {
                Header = LocalizedStrings.Instances_ResourceManagementPage__DownloadHeader,
                Description = LocalizedStrings.Instances_ResourceManagementPage__DownloadDescription,
                HeaderIcon = new FontIcon { Glyph = "\uE896" },
                Command = viewModel.DownloadResourcesCommand,
                IsClickEnabled = true
            });
        }

        if (viewModel.Items.Count == 0)
        {
            RootPanel.Children.Add(CreateEmptyState(viewModel.EmptyText));
            return;
        }

        foreach (var item in viewModel.Items)
            RootPanel.Children.Add(CreateResourceCard(viewModel, item));
    }

    private static StackPanel CreateEmptyState(string emptyText)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(0, 60, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 4
        };

        var title = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        title.Children.Add(new FontIcon { FontSize = 32, Glyph = "\uE74C" });
        title.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, -4, 0, 0),
            FontSize = 28,
            Style = TryGetResource<Style>("BaseTextBlockStyle"),
            Text = emptyText
        });

        panel.Children.Add(title);
        return panel;
    }

    private SettingsCard CreateResourceCard(ResourceManagementViewModel viewModel, ManagedResourceItem item)
    {
        var card = new SettingsCard
        {
            Padding = new Thickness(16, 8, 16, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Header = CreateResourceHeader(item),
            Command = viewModel.OpenItemCommand,
            CommandParameter = item,
            IsClickEnabled = true,
            ContextFlyout = CreateItemFlyout(viewModel, item)
        };

        return card;
    }

    private static Grid CreateResourceHeader(ManagedResourceItem item)
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconBorder = new Border
        {
            Width = 48,
            Height = 48,
            VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = TryGetResource<Brush>("IconBorder"),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4)
        };

        if (item.HasIcon)
        {
            iconBorder.Background = new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(item.IconPath)),
                Stretch = Stretch.UniformToFill
            };
        }
        else
        {
            iconBorder.Child = new FontIcon { FontSize = 24, Glyph = item.IconGlyph };
        }

        root.Children.Add(iconBorder);

        var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(textPanel, 2);
        textPanel.Children.Add(new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Text = item.Name,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        textPanel.Children.Add(new TextBlock
        {
            Foreground = TryGetResource<Brush>("ApplicationSecondaryForegroundThemeBrush"),
            MaxLines = 1,
            Style = TryGetResource<Style>("CaptionTextBlockStyle"),
            Text = item.Description,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        });
        textPanel.Children.Add(new TextBlock
        {
            Foreground = TryGetResource<Brush>("ApplicationSecondaryForegroundThemeBrush"),
            MaxLines = 1,
            Style = TryGetResource<Style>("CaptionTextBlockStyle"),
            Text = item.Path,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        });

        root.Children.Add(textPanel);
        return root;
    }

    private static MenuFlyout CreateItemFlyout(ResourceManagementViewModel viewModel, ManagedResourceItem item)
    {
        var flyout = new MenuFlyout { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom };
        flyout.Items.Add(new MenuFlyoutItem
        {
            Text = LocalizedStrings.Buttons_Open_Text,
            Icon = new FontIcon { Glyph = "\uEC51" },
            Command = viewModel.OpenItemCommand,
            CommandParameter = item
        });

        if (item.CanBackup)
        {
            flyout.Items.Add(new MenuFlyoutItem
            {
                Text = LocalizedStrings.Instances_ResourceManagementPage__Backup,
                Icon = new FontIcon { Glyph = "\uE8F4" },
                Command = viewModel.BackupItemCommand,
                CommandParameter = item
            });
        }

        flyout.Items.Add(new MenuFlyoutItem
        {
            Text = LocalizedStrings.Buttons_Delete_Text,
            Icon = new SymbolIcon(Symbol.Delete),
            Command = viewModel.ConfirmDeleteCommand,
            CommandParameter = item
        });

        return flyout;
    }

    private static T? TryGetResource<T>(string key) where T : class
    {
        try
        {
            return Application.Current.Resources.TryGetValue(key, out object value) ? value as T : null;
        }
        catch
        {
            return null;
        }
    }

    private void Page_DragEnter(object sender, DragEventArgs e)
    {
        if (VM.AllowImport && e.DataView.Contains(StandardDataFormats.StorageItems))
            e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (!VM.AllowImport || !e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        e.AcceptedOperation = DataPackageOperation.Copy;
        var paths = new List<string>();

        foreach (var item in await e.DataView.GetStorageItemsAsync())
            paths.Add(item.Path);

        await VM.ImportAsync(paths);
    }

}
