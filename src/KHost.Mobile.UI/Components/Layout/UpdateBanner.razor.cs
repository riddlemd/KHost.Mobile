namespace KHost.Mobile.UI.Components.Layout;

public sealed partial class UpdateBanner
{
    private AppUpdateStatus _status = AppUpdateStatus.None;

    protected override async Task OnInitializedAsync()
    {
        // Memoized in the service, so this only hits the network on the first component to ask this launch.
        _status = await Updates.GetStatusAsync();
    }

    private async Task OpenReleaseAsync()
    {
        if (_status.ReleaseUrl is { } url)
            await Links.OpenAsync(url);
    }

    private void Dismiss() => Updates.Dismiss();
}
