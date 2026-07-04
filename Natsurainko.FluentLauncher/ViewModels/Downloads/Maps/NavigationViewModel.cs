using FluentLauncher.Infra.UI.Navigation;

namespace Natsurainko.FluentLauncher.ViewModels.Downloads.Maps;

internal partial class NavigationViewModel(INavigationService navigationService)
    : NavigationPageVM(navigationService), INavigationAware
{
    protected override string RootPageKey => "MapsDownload";

    void INavigationAware.OnNavigatedTo(object? parameter) => NavigateTo("MapsDownload/Default", parameter);
}
