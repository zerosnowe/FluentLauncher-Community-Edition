using Microsoft.UI.Xaml.Controls;
using Natsurainko.FluentLauncher.Utils.Extensions;
using Nrk.FluentCore.GameManagement;
using Nrk.FluentCore.GameManagement.Installer;
using Nrk.FluentCore.GameManagement.Instances;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using StringComparer = Natsurainko.FluentLauncher.Utils.StringComparer;

namespace Natsurainko.FluentLauncher.Services.UI;

public partial class SearchProviderService
{
    private bool _isInitialized = false;
    private AutoSuggestBox _autoSuggestBox = null!;

    public object? CurrentOwner { get; private set; }

    public bool IsBinded { get; private set; }

    public void Initialize(AutoSuggestBox autoSuggestBox)
    {
        _autoSuggestBox = autoSuggestBox;
        _autoSuggestBox.SuggestionChosen += AutoSuggestBox_SuggestionChosen;
        //_autoSuggestBox.TextChanged += AutoSuggestBox_TextChanged;
        //_autoSuggestBox.QuerySubmitted += AutoSuggestBox_QuerySubmitted;

        _autoSuggestBox.UpdateTextOnSelect = false;
        _autoSuggestBox.IsEnabled = false;
        _autoSuggestBox.SetValue(TextBox.IsSpellCheckEnabledProperty, false);

        _isInitialized = true;
    }

    private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is Suggestion suggestion)
        {
            suggestion.InvokeAction?.Invoke();
        }
    }

    public BindedSearchProvider BindProvider(object owner)
    {
        if (!_isInitialized) throw new InvalidOperationException("SearchProviderService is not initialized.");
        if (IsBinded) throw new InvalidOperationException("SearchProviderService is already binded to another owner.");

        return new(this, owner);
    }

    public partial class BindedSearchProvider : IDisposable
    {
        private readonly SearchProviderService _service;
        private bool _suggestionsSourceBinded = false;
        private bool _querySubmitionBinded = false;

        private IDisposable? _suggestionsSourceBinding;
        private IDisposable? _querySubmitionBinding;

        public AutoSuggestBox SearchBox => _service._autoSuggestBox;

        public BindedSearchProvider(SearchProviderService service, object owner)
        {
            _service = service;
            _service.CurrentOwner = owner;
            _service.IsBinded = true;

            SearchBox.IsEnabled = true;
        }

        public void BindQuerySubmition(Action<string> action)
        {
            if (_querySubmitionBinded)
                throw new InvalidOperationException("Query submition already binded.");

            _querySubmitionBinding = Observable.FromEventPattern<TypedEventHandler<AutoSuggestBox, AutoSuggestBoxQuerySubmittedEventArgs>, AutoSuggestBoxQuerySubmittedEventArgs>(
                handler => SearchBox.QuerySubmitted += handler,
                handler => SearchBox.QuerySubmitted -= handler)
                .Where(e => e.EventArgs.ChosenSuggestion is null)
                .Select(e => ((AutoSuggestBox)e.Sender!).Text.Trim())
                //.Where(query => !string.IsNullOrEmpty(query))
                .ObserveOnDispatcherQueue(App.DispatcherQueue)
                .Subscribe(action);

            _querySubmitionBinded = true;
        }

        public void BindSuggestionsSource(Func<string, IEnumerable<Suggestion>> func)
        {
            if (_suggestionsSourceBinded)
                throw new InvalidOperationException("Suggestions source already binded.");

            _suggestionsSourceBinding = Observable.FromEventPattern<TypedEventHandler<AutoSuggestBox, AutoSuggestBoxTextChangedEventArgs>, AutoSuggestBoxTextChangedEventArgs>(
                handler => SearchBox.TextChanged += handler,
                handler => SearchBox.TextChanged -= handler)
                .Throttle(TimeSpan.FromSeconds(0.15))
                .ObserveOnDispatcherQueue(App.DispatcherQueue)
                //.Where(e => e.EventArgs.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                .Select(e => ((AutoSuggestBox)e.Sender!).Text.Trim())
                //.Where(query => !string.IsNullOrEmpty(query))
                .DistinctUntilChanged()
                .SubscribeOnDispatcherQueue(App.DispatcherQueue)
                .Subscribe(query => SearchBox.ItemsSource = func(query));

            _suggestionsSourceBinded = true;
        }

        public void ClearInput() => SearchBox.Text = string.Empty;

        public void Dispose()
        {
            if (_suggestionsSourceBinded)
            {
                _suggestionsSourceBinding?.Dispose();
                _suggestionsSourceBinded = false;
            }

            if (_querySubmitionBinded)
            {
                _querySubmitionBinding?.Dispose();
                _querySubmitionBinded = false;
            }

            _service.CurrentOwner = null;
            _service.IsBinded = false;

            ClearInput();
            SearchBox.IsEnabled = false;
            SearchBox.ItemsSource = null;
        }
    }
}

public class Suggestion
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    public SuggestionIconType SuggestionIconType { get; set; } = SuggestionIconType.Glyph;

    public string Icon { get; set; } = "\ue721";

    public Action? InvokeAction { get; set; }

    public object? Parameter { get; set; }
}

public enum SuggestionIconType
{
    Glyph = 0,
    UriIcon = 1,
    WebUrlIcon = 2
}

internal static class SuggestionHelper
{
    public static List<(string, string)>? CurseforgeModSearchSlugs;
    public static List<(string, string)>? ModrinthModSearchSlugs;

    static SuggestionHelper()
    {
        Task.Run(async () => CurseforgeModSearchSlugs = await LoadSearchSlugs("curseforge-mod-slugs.json")).Forget();
        Task.Run(async () => ModrinthModSearchSlugs = await LoadSearchSlugs("modrinth-mod-slugs.json")).Forget();
    }

    public static Suggestion FromMinecraftInstance(MinecraftInstance instance, string description, Action action)
    {
        return new Suggestion
        {
            Title = instance.InstanceId,
            Description = description,
            SuggestionIconType = SuggestionIconType.UriIcon,
            Icon = string.Format("ms-appx:///Assets/Icons/{0}.png", instance.Version.Type switch
            {
                MinecraftVersionType.Release => "grass_block_side",
                MinecraftVersionType.Snapshot => "crafting_table_front",
                MinecraftVersionType.OldBeta => "dirt_path_side",
                MinecraftVersionType.OldAlpha => "dirt_path_side",
                _ => "grass_block_side"
            }),
            InvokeAction = action
        };
    }

    public static Suggestion FromVersionManifestItem(VersionManifestItem item, string description, Action action)
    {
        return new Suggestion
        {
            Title = item.Id,
            Description = description,
            SuggestionIconType = SuggestionIconType.UriIcon,
            Icon = string.Format("ms-appx:///Assets/Icons/{0}.png", item.Type switch
            {
                "release" => "grass_block_side",
                "snapshot" => "crafting_table_front",
                "old_beta" => "dirt_path_side",
                "old_alpha" => "dirt_path_side",
                _ => "grass_block_side"
            }),
            InvokeAction = action
        };
    }

    public static IEnumerable<Suggestion> GetSearchModSuggestions(string query, int source, Action<string> action)
    {
        List<(string, string)>? slugs = source switch
        {
            0 => CurseforgeModSearchSlugs,
            1 => ModrinthModSearchSlugs,
            _ => null
        };

        if (slugs is null) return [];

        return slugs.Where(i => i.Item1.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(i => i.Item1.StartsWith(query))
            .ThenBy(i => StringComparer.LevenshteinDistance(i.Item1, query))
            .Select(i => new Suggestion
            {
                Title = i.Item1,
                Description = i.Item2,
                InvokeAction = () => action(i.Item2)
            });
    }

    private static async Task<List<(string, string)>> LoadSearchSlugs(string slugFileName)
    {
        string json = await File.ReadAllTextAsync(Path.Combine(Package.Current.InstalledLocation.Path, $"Assets\\Strings\\{slugFileName}"));

        var array = JsonNode.Parse(json)!.AsArray();
        var list = new List<(string, string)>(array.Count);

        foreach (var item in array)
        {
            list.Add((
                item!["name"]!.GetValue<string>(),
                item!["slug"]!.GetValue<string>()
            ));
        }

        return list;
    }
}