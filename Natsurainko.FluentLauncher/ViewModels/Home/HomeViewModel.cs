using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FluentLauncher.Infra.UI.Dialogs;
using FluentLauncher.Infra.UI.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Natsurainko.FluentLauncher.Models;
using Natsurainko.FluentLauncher.Services.Accounts;
using Natsurainko.FluentLauncher.Services.Launch;
using Natsurainko.FluentLauncher.Services.Network;
using Natsurainko.FluentLauncher.Services.Settings;
using Natsurainko.FluentLauncher.Services.UI;
using Natsurainko.FluentLauncher.Services.UI.Messaging;
using Natsurainko.FluentLauncher.Utils;
using Natsurainko.FluentLauncher.Utils.Extensions;
using Nrk.FluentCore.Authentication;
using Nrk.FluentCore.GameManagement.Instances;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Numerics;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Windows.System;
using static Natsurainko.FluentLauncher.Services.UI.SearchProviderService;
using Microsoft.UI;

#nullable disable
namespace Natsurainko.FluentLauncher.ViewModels.Home;

internal partial class HomeViewModel : PageVM, INavigationAware,
    IRecipient<TrackLaunchTaskChangedMessage>, 
    IRecipient<ActiveAccountChangedMessage>
{
    private readonly GameService _gameService;
    private readonly AccountService _accountService;
    private readonly LaunchService _launchService;
    private readonly CacheInterfaceService _cacheInterfaceService;
    private readonly SettingsService _settingsService;
    private readonly SearchProviderService _searchProviderService;
    private readonly IDialogActivationService<ContentDialogResult> _dialogService;

    private bool _registeredListener = false;
    private static LaunchTaskViewModel _trackingTask = null;
    private BindedSearchProvider _bindedSearchProvider;
    private string _newsJson = string.Empty;

    public ReadOnlyObservableCollection<MinecraftInstance> MinecraftInstances { get; private set; }

    public ReadOnlyObservableCollection<Account> Accounts { get; init; }

    public HomeViewModel(
        GameService gameService,
        AccountService accountService,
        LaunchService launchService,
        CacheInterfaceService cacheInterfaceService,
        SettingsService settingsService,
        SearchProviderService searchProviderService,
        IDialogActivationService<ContentDialogResult> dialogService)
    {
        _accountService = accountService;
        _gameService = gameService;
        _launchService = launchService;
        _cacheInterfaceService = cacheInterfaceService;
        _settingsService = settingsService;
        _searchProviderService = searchProviderService;
        _dialogService = dialogService;

        Accounts = accountService.Accounts;
        ActiveAccount = accountService.ActiveAccount;

        MinecraftInstances = _gameService.Games;
        ActiveMinecraftInstance = _gameService.ActiveGame;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountTag))]
    public partial Account ActiveAccount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstanceSelectorText))]
    public partial MinecraftInstance ActiveMinecraftInstance { get; set; }

    [ObservableProperty]
    public partial LaunchTaskViewModel TrackingTask { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaunchButtonIcon))]
    public partial bool IsTrackingTask { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LatestNewsTitle))]
    [NotifyPropertyChangedFor(nameof(LatestNewsImageUrl))]
    [NotifyPropertyChangedFor(nameof(NewsCardVisibility))]
    public partial NewsData LatestNewsData { get; set; }

    [ObservableProperty]
    public partial NewsData[] PreviousNewsDatas { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewsCarouselCount))]
    public partial NewsData[] NewsCarouselDatas { get; set; } = [];

    [ObservableProperty]
    public partial int CurrentNewsIndex { get; set; } = 0;

    [ObservableProperty]
    public partial Vector3 LaunchingInfoGridVector3 { get; set; } = new(480, 0, 0);

    [ObservableProperty]
    public partial Vector3 InstanceSelectorGridVector3 { get; set; } = new();

    [ObservableProperty]
    public partial double LaunchingInfoGridOpacity { get; set; } = 0;

    [ObservableProperty]
    public partial double InstanceSelectorGridOpacity { get; set; } = 1;

    [ObservableProperty]
    public partial string LaunchButtonText { get; set; } = LocalizedStrings.Home_HomePage_LaunchButton_Text;

    [ObservableProperty]
    public partial Brush CustomBackgroundBrush { get; set; } = new SolidColorBrush(Colors.Transparent);

    public Visibility AccountTag => ActiveAccount is null ? Visibility.Collapsed : Visibility.Visible;

    public string InstanceSelectorText => ActiveMinecraftInstance == null
        ? LocalizedStrings.Home_HomePage__NoInstanceSelected
        : ActiveMinecraftInstance.GetDisplayName();

    public string LaunchButtonIcon => IsTrackingTask ? "\uEE95" : "\uF5B0";

    public string LatestNewsTitle => LatestNewsData?.Title ?? string.Empty;

    public string LatestNewsImageUrl => LatestNewsData?.ImageUrl ?? string.Empty;

    public Visibility NewsCardVisibility => LatestNewsData is null ? Visibility.Collapsed : Visibility.Visible;

    public int NewsCarouselCount => NewsCarouselDatas?.Length ?? 0;

    partial void OnIsTrackingTaskChanged(bool value)
    {
        if (IsTrackingTask)
        {
            InstanceSelectorGridVector3 = new Vector3(Convert.ToSingle(App.MainWindow.Width) + 120, 0, 0);
            LaunchingInfoGridVector3 = new Vector3(0, 0, 0);

            InstanceSelectorGridOpacity = 0;
            LaunchingInfoGridOpacity = 1;
        }
        else
        {
            LaunchingInfoGridVector3 = new Vector3(480, 0, 0);
            InstanceSelectorGridVector3 = new Vector3(0, 0, 0);

            InstanceSelectorGridOpacity = 1;
            LaunchingInfoGridOpacity = 0;
        }
    }

    partial void OnActiveMinecraftInstanceChanged(MinecraftInstance value)
    {
        if (value is not null)
            _gameService.ActivateGame(value);
    }

    partial void OnActiveAccountChanged(Account value) => _accountService.ActivateAccount(value);

    [RelayCommand(CanExecute = nameof(CanExecuteLaunch))]
    async Task Launch()
    {
        if (_settingsService.HomePageLaunchButtonBehavior == 0)
        {
            _launchService.LaunchFromUI(ActiveMinecraftInstance);
            return;
        }

        if (IsTrackingTask)
        {
            if (TrackingTask.ProcessLaunched)
                TrackingTask.KillProcess();
            else if (TrackingTask.CanCancel)
                await TrackingTask.Cancel();

            return;
        }

        _launchService.LaunchFromUIWithTrack(ActiveMinecraftInstance);
    }

    [RelayCommand]
    void GoToInstancesManage() => GlobalNavigate("Instances/Navigation");

    [RelayCommand]
    void GoToAccountSettings() => GlobalNavigate("Settings/Navigation", "Settings/Account");

    [RelayCommand]
    async Task AddAccount() => await _dialogService.ShowAsync("AuthenticationWizardDialog");

    [RelayCommand]
    void Continue() => WeakReferenceMessenger.Default.Send(new TrackLaunchTaskChangedMessage(null));

    [RelayCommand]
    void ShowDetails() => GlobalNavigate("Tasks/Launch");

    [RelayCommand]
    async Task OpenNews(NewsData newsData)
    {
        if (newsData?.ReadMoreUrl is not string url || string.IsNullOrWhiteSpace(url))
            return;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            await Launcher.LaunchUriAsync(uri);
    }

    void TrackingTask_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "ProcessLaunched")
            UpdateLaunchButtonText();
    }

    IEnumerable<Suggestion> ProviderSuggestions(string searchText)
    {
        yield return new Suggestion
        {
            Title = LocalizedStrings.SearchSuggest__T1.Replace("{searchText}", searchText),
            Description = LocalizedStrings.SearchSuggest__D1,
            InvokeAction = () => GlobalNavigate("InstancesDownload/Navigation", searchText)
        };

        foreach (var item in MinecraftInstances)
        {
            if (item.InstanceId.Contains(searchText))
            {
                yield return SuggestionHelper.FromMinecraftInstance(item,
                    LocalizedStrings.SearchSuggest__D4,
                    () => _launchService.LaunchFromUI(item));
            }
        }
    }

    void INavigationAware.OnNavigatedTo(object parameter)
    {
        _bindedSearchProvider = _searchProviderService.BindProvider(this);
        _bindedSearchProvider.BindSuggestionsSource(ProviderSuggestions);

        _cacheInterfaceService.RequestStringAsync(
            CacheInterfaceService.LauncherContentNews,
            Services.Network.Data.InterfaceRequestMethod.PreferredLocal,
            ParseNewsTask,
            "cache-interfaces\\launchercontent.mojang.com\\news.json")
            .ContinueWith(ParseNewsTask);

        App.MainWindow.SizeChanged += SizeChanged;

        if (_trackingTask != null && _trackingTask.TaskState == TaskState.Running)
        {
            TrackingTask = _trackingTask;
            IsTrackingTask = true;
            UpdateLaunchButtonText();

            TrackingTask.PropertyChanged += TrackingTask_PropertyChanged;
            _registeredListener = true;
        }
        else _trackingTask = null;

#if FLUENT_LAUNCHER_PREVIEW_CHANNEL
        App.GetService<Services.Network.UpdateService>().CheckLaunchUpdateAfterApplicationStarted(_dialogService);
#endif
    }

    void INavigationAware.OnNavigatedFrom()
    {
        _bindedSearchProvider.Dispose();

        if (_registeredListener)
        {
            TrackingTask.PropertyChanged -= TrackingTask_PropertyChanged;
            _registeredListener = false;
        }

        App.MainWindow.SizeChanged -= SizeChanged;
    }

    void IRecipient<TrackLaunchTaskChangedMessage>.Receive(TrackLaunchTaskChangedMessage message)
    {
        if (_registeredListener)
        {
            TrackingTask.PropertyChanged -= TrackingTask_PropertyChanged;
            _registeredListener = false;
        }

        _trackingTask = message.Value;

        Dispatcher.TryEnqueue(() =>
        {
            TrackingTask = message.Value;
            IsTrackingTask = message.Value != null;
            UpdateLaunchButtonText();

            if (IsTrackingTask)
            {
                TrackingTask.PropertyChanged += TrackingTask_PropertyChanged;
                _registeredListener = true;
            }
        });
    }

    void IRecipient<ActiveAccountChangedMessage>.Receive(ActiveAccountChangedMessage message)
        => Dispatcher.TryEnqueue(() => ActiveAccount = message.Value);

    void SizeChanged(object s, WindowSizeChangedEventArgs e)
    {
        if (!IsTrackingTask) return;

        InstanceSelectorGridVector3 = new Vector3(Convert.ToSingle(App.MainWindow.Width) + 120, 0, 0);
    }

    bool CanExecuteLaunch() => ActiveMinecraftInstance is not null;

    void ParseNewsTask(Task<string> task)
    {
        if (task is null)
            return;

        // CacheInterfaceService.PreferredLocal 会在后台启动下载任务：这里必须“观察”异常，避免调试时被当成未处理异常抛出。
        if (!task.IsCompleted)
            return;

        if (task.IsFaulted)
        {
            _ = task.Exception;
            return;
        }

        if (task.IsCanceled)
            return;

        string newsJson = task.Result;
        if (string.IsNullOrEmpty(newsJson) || _newsJson == newsJson)
            return;

        var newsDatas = JsonNode.Parse(newsJson)!["entries"]?.AsArray().Select(node =>
        {
            var newsData = node.Deserialize(FLSerializerContext.Default.NewsData);
            newsData.ImageUrl = $"https://launchercontent.mojang.com{node["newsPageImage"]!["url"]!.GetValue<string>()}";
            return newsData;
        }).ToArray() ?? [];

        if (newsDatas.Length == 0)
            return;

        // Keep scroll order stable by publish date (newest -> older). Only use the first 10 images for carousel.
        var ordered = newsDatas
            .Select((d, i) => (Data: d, Index: i, Date: TryParseNewsDate(d.Date)))
            .OrderByDescending(x => x.Date ?? DateTime.MinValue)
            .ThenBy(x => x.Index)
            .Select(x => x.Data)
            .ToArray();

        var carousel = ordered.Take(10).ToArray();
        var latest = carousel.Length > 0 ? carousel[0] : ordered[0];
        var previous = ordered.Skip(1).Take(3).ToArray();

        _newsJson = newsJson;
        Dispatcher.TryEnqueue(() =>
        {
            LatestNewsData = latest;
            PreviousNewsDatas = previous;
            NewsCarouselDatas = carousel;
            if (CurrentNewsIndex >= (carousel.Length == 0 ? 1 : carousel.Length))
                CurrentNewsIndex = 0;
        });
    }

    private static DateTime? TryParseNewsDate(string? date)
    {
        if (string.IsNullOrWhiteSpace(date))
            return null;

        if (DateTime.TryParse(date, out var dt))
            return dt;

        return null;
    }

    void UpdateLaunchButtonText()
    {
        if (IsTrackingTask)
        {
            if (TrackingTask.ProcessLaunched)
                LaunchButtonText = LocalizedStrings.Home_HomePage__KillProcess.Replace("Minecraft", TrackingTask.Title);
            else LaunchButtonText = LocalizedStrings.Home_HomePage__CancelLaunch.Replace("Minecraft", TrackingTask.Title);

            return;
        }

        LaunchButtonText = LocalizedStrings.Home_HomePage_LaunchButton_Text;
    }
}
