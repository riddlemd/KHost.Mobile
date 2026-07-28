using KHost.Mobile.Abstractions.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace KHost.Mobile.Services;

/// <inheritdoc />
public sealed class MauiFileExchange : IFileExchange
{
    private static readonly FilePickerFileType JsonFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.Android] = ["application/json"],
        [DevicePlatform.iOS] = ["public.json", "public.text"],
        [DevicePlatform.WinUI] = [".json"],
        [DevicePlatform.macOS] = ["json"],
    });

    /// <inheritdoc />
    public async Task<string?> PickJsonTextAsync(string pickerTitle, CancellationToken cancellationToken = default)
    {
        var pick = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = pickerTitle, FileTypes = JsonFileType });
        if (pick is null)
            return null;

        // OpenReadAsync, never .FullPath — on Android the pick is a content:// URI with no readable path.
        await using var stream = await pick.OpenReadAsync();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ShareJsonAsync(string fileName, string json, string title, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        await Share.Default.RequestAsync(new ShareFileRequest { Title = title, File = new ShareFile(path) });
    }
}
