using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using FluentLauncher.Infra.UI.Navigation;
using FluentLauncher.Infra.UI.Notification;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Natsurainko.FluentLauncher.Services.UI.Notification;
using Natsurainko.FluentLauncher.Utils;
using Natsurainko.FluentLauncher.Utils.Extensions;
using Natsurainko.FluentLauncher.Views.Downloads;
using Nrk.FluentCore.GameManagement.Instances;
using Nrk.FluentCore.GameManagement.Saves;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

#nullable disable
namespace Natsurainko.FluentLauncher.ViewModels.Instances;

internal partial class ResourceManagementViewModel(INotificationService notificationService) : PageVM, INavigationAware
{
    private string _navigationKey;

    public MinecraftInstance MinecraftInstance { get; private set; }

    public ObservableCollection<ManagedResourceItem> Items { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string FolderTitle { get; set; }

    [ObservableProperty]
    public partial string FolderPath { get; set; }

    [ObservableProperty]
    public partial string EmptyText { get; set; }

    [ObservableProperty]
    public partial string DownloadPageKey { get; set; }

    [ObservableProperty]
    public partial bool ShowDownload { get; set; }

    [ObservableProperty]
    public partial bool AllowImport { get; set; }

    public ResourceKind Kind { get; private set; }

    void INavigationAware.SetNavigationKey(string key) => _navigationKey = key;

    void INavigationAware.OnNavigatedTo(object parameter)
    {
        MinecraftInstance = parameter as MinecraftInstance;
        ConfigureFromNavigationKey();
        Task.Run(LoadResourcesAsync).Forget();
    }

    void ConfigureFromNavigationKey()
    {
        Kind = _navigationKey switch
        {
            "Instances/ResourcePacks" => ResourceKind.ResourcePacks,
            "Instances/Shaders" => ResourceKind.Shaders,
            "Instances/Screenshots" => ResourceKind.Screenshots,
            "Instances/Maps" => ResourceKind.Maps,
            "Instances/Servers" => ResourceKind.Servers,
            _ => ResourceKind.ResourcePacks
        };

        string gameDirectory = MinecraftInstance.GetGameDirectory();

        (Title, FolderTitle, FolderPath, EmptyText, DownloadPageKey, ShowDownload, AllowImport) = Kind switch
        {
            ResourceKind.ResourcePacks => (LocalizedStrings.Instances_ResourceManagementPage__ResourcePacksTitle, LocalizedStrings.Instances_ResourceManagementPage__ResourcePacksFolder, Path.Combine(gameDirectory, "resourcepacks"), LocalizedStrings.Instances_ResourceManagementPage__ResourcePacksEmpty, "TexturePacksDownload/Navigation", true, true),
            ResourceKind.Shaders => (LocalizedStrings.Instances_ResourceManagementPage__ShadersTitle, LocalizedStrings.Instances_ResourceManagementPage__ShadersFolder, Path.Combine(gameDirectory, "shaderpacks"), LocalizedStrings.Instances_ResourceManagementPage__ShadersEmpty, "ShadersDownload/Navigation", true, true),
            ResourceKind.Screenshots => (LocalizedStrings.Instances_ResourceManagementPage__ScreenshotsTitle, LocalizedStrings.Instances_ResourceManagementPage__ScreenshotsFolder, Path.Combine(gameDirectory, "screenshots"), LocalizedStrings.Instances_ResourceManagementPage__ScreenshotsEmpty, string.Empty, false, true),
            ResourceKind.Maps => (LocalizedStrings.Instances_ResourceManagementPage__MapsTitle, LocalizedStrings.Instances_ResourceManagementPage__MapsFolder, MinecraftInstance.GetSavesDirectory(), LocalizedStrings.Instances_ResourceManagementPage__MapsEmpty, "MapsDownload/Navigation", true, true),
            ResourceKind.Servers => (LocalizedStrings.Instances_ResourceManagementPage__ServersTitle, LocalizedStrings.Instances_ResourceManagementPage__ServersFolder, gameDirectory, LocalizedStrings.Instances_ResourceManagementPage__ServersEmpty, string.Empty, false, true),
            _ => throw new NotImplementedException()
        };

        if (Kind != ResourceKind.Servers)
            Directory.CreateDirectory(FolderPath);
    }

    public async Task LoadResourcesAsync()
    {
        List<ManagedResourceItem> items = Kind switch
        {
            ResourceKind.Maps => await LoadMapsAsync(),
            ResourceKind.Servers => LoadServers(),
            _ => LoadFiles()
        };

        await Dispatcher.EnqueueAsync(() =>
        {
            Items.Clear();
            foreach (var item in items)
                Items.Add(item);
        });
    }

    List<ManagedResourceItem> LoadFiles()
    {
        if (!Directory.Exists(FolderPath))
            return [];

        var items = new List<ManagedResourceItem>();
        foreach (var directory in Directory.EnumerateDirectories(FolderPath).OrderBy(Path.GetFileName))
        {
            if (Kind == ResourceKind.Screenshots)
                continue;

            items.Add(CreateFileItem(directory, true));
        }

        foreach (var file in Directory.EnumerateFiles(FolderPath).Where(IsSupportedFile).OrderBy(Path.GetFileName))
            items.Add(CreateFileItem(file, false));

        return items;
    }

    async Task<List<ManagedResourceItem>> LoadMapsAsync()
    {
        var items = new List<ManagedResourceItem>();
        var saveManager = new SaveManager(FolderPath);

        await foreach (var saveInfo in saveManager.EnumerateSavesAsync())
        {
            items.Add(new ManagedResourceItem
            {
                Name = saveInfo.LevelName,
                Description = $"{saveInfo.FolderName} | {saveInfo.Version} | {saveInfo.LastPlayed:g}",
                Path = saveInfo.Folder,
                IconGlyph = "\uE81E",
                IconPath = saveInfo.IconFilePath,
                IsDirectory = true,
                Kind = Kind
            });
        }

        return items;
    }

    List<ManagedResourceItem> LoadServers()
    {
        string serversFile = GetServersFile();
        if (!File.Exists(serversFile))
            return [];

        var fileInfo = new FileInfo(serversFile);
        return
        [
            new ManagedResourceItem
            {
                Name = "servers.dat",
                Description = $"{LongExtensions.ToFileSizeString(fileInfo.Length)} | {fileInfo.LastWriteTime:g}",
                Path = serversFile,
                IconGlyph = "\uE774",
                Kind = Kind,
                CanBackup = true
            }
        ];
    }

    ManagedResourceItem CreateFileItem(string path, bool isDirectory)
    {
        string name = Path.GetFileName(path);
        string description = isDirectory ? LocalizedStrings.Instances_ResourceManagementPage__FolderItem : LongExtensions.ToFileSizeString(new FileInfo(path).Length);
        string iconGlyph = Kind switch
        {
            ResourceKind.ResourcePacks => "\uE8B7",
            ResourceKind.Shaders => "\uE790",
            ResourceKind.Screenshots => "\uE8B9",
            _ => "\uE8A5"
        };

        if (!isDirectory)
            description += $" | {File.GetLastWriteTime(path):g}";

        return new ManagedResourceItem
        {
            Name = name,
            Description = description,
            Path = path,
            IconGlyph = iconGlyph,
            IconPath = Kind == ResourceKind.Screenshots ? path : null,
            IsDirectory = isDirectory,
            Kind = Kind
        };
    }

    bool IsSupportedFile(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return Kind switch
        {
            ResourceKind.ResourcePacks or ResourceKind.Shaders => extension == ".zip",
            ResourceKind.Screenshots => extension is ".png" or ".jpg" or ".jpeg",
            _ => false
        };
    }

    string GetServersFile() => Path.Combine(MinecraftInstance.GetGameDirectory(), "servers.dat");

    [RelayCommand]
    void OpenFolder() => ExplorerHelper.OpenFolder(FolderPath);

    [RelayCommand]
    void DownloadResources()
    {
        if (!string.IsNullOrEmpty(DownloadPageKey))
            GlobalNavigate(DownloadPageKey);
    }

    [RelayCommand]
    void OpenItem(ManagedResourceItem item)
    {
        if (item.Kind == ResourceKind.Screenshots && File.Exists(item.Path))
        {
            new ScreenshotPreviewWindow(new Uri(item.Path).AbsoluteUri).Activate();
            return;
        }

        if (item.IsDirectory)
            ExplorerHelper.OpenFolder(item.Path);
        else ExplorerHelper.ShowAndSelectFile(item.Path);
    }

    [RelayCommand]
    void ConfirmDelete(ManagedResourceItem item)
    {
        notificationService.Show(new ConfirmNotification
        {
            Title = LocalizedStrings.Instances_ResourceManagementPage__DeleteTitle,
            Message = item.Path,
            ActionButtonCommand = DeleteItemCommand,
            ActionButtonCommandParameter = item,
            ActionButtonStyle = App.Current.Resources["DeleteButtonStyle"] as Style,
            ActionButtonContent = new TextBlock
            {
                Text = LocalizedStrings.Buttons_Delete_Text,
                Foreground = new SolidColorBrush(Colors.White)
            },
        });
    }

    [RelayCommand]
    void DeleteItem(ManagedResourceItem item)
    {
        if (item.IsDirectory)
            Directory.Delete(item.Path, true);
        else if (File.Exists(item.Path))
            File.Delete(item.Path);

        Task.Run(LoadResourcesAsync).Forget();
    }

    [RelayCommand]
    void BackupItem(ManagedResourceItem item)
    {
        if (!File.Exists(item.Path))
            return;

        string backupPath = $"{item.Path}.{DateTime.Now:yyyyMMddHHmmss}.bak";
        File.Copy(item.Path, backupPath, true);
        Task.Run(LoadResourcesAsync).Forget();
    }

    public async Task<int> ImportAsync(IEnumerable<string> paths)
    {
        int count = 0;

        foreach (string path in paths)
        {
            if (Kind == ResourceKind.Servers)
            {
                if (File.Exists(path) && Path.GetFileName(path).Equals("servers.dat", StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(path, GetServersFile(), true);
                    count++;
                }

                continue;
            }

            if (Directory.Exists(path))
            {
                if (Kind == ResourceKind.Screenshots)
                    continue;

                if (Kind == ResourceKind.Maps && !File.Exists(Path.Combine(path, "level.dat")))
                    continue;

                CopyDirectory(path, Path.Combine(FolderPath, Path.GetFileName(path)), true);
                count++;
            }
            else if (File.Exists(path))
            {
                if (Kind == ResourceKind.Maps && Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    ImportMapZip(path);
                    count++;
                }
                else if (IsSupportedFile(path))
                {
                    File.Copy(path, Path.Combine(FolderPath, Path.GetFileName(path)), true);
                    count++;
                }
            }
        }

        if (count > 0)
            await LoadResourcesAsync();

        return count;
    }

    void ImportMapZip(string path)
    {
        string target = Path.Combine(FolderPath, Path.GetFileNameWithoutExtension(path));
        if (Directory.Exists(target))
            target = $"{target}-{DateTime.Now:yyyyMMddHHmmss}";

        ZipFile.ExtractToDirectory(path, target);
    }

    static void CopyDirectory(string source, string target, bool overwrite)
    {
        Directory.CreateDirectory(target);

        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(source, target));

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, target), overwrite);
    }
}

internal partial class ManagedResourceItem : ObservableObject
{
    public string Name { get; init; }

    public string Description { get; init; }

    public string Path { get; init; }

    public string IconGlyph { get; init; }

    public string IconPath { get; init; }

    public bool HasIcon => !string.IsNullOrEmpty(IconPath) && File.Exists(IconPath);

    public bool IsDirectory { get; init; }

    public bool CanBackup { get; init; }

    public ResourceKind Kind { get; init; }
}

internal enum ResourceKind
{
    ResourcePacks,
    Shaders,
    Screenshots,
    Maps,
    Servers
}
