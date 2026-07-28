namespace KHost.Mobile.Abstractions.Services;

/// <summary>
/// Moves a JSON file between the app and wherever the user keeps files, through the platform's own picker and
/// share sheet. The app never uploads anything — the OS decides where a shared file goes.
/// </summary>
public interface IFileExchange
{
    /// <summary>
    /// Asks the user for a JSON file and returns its text, or <c>null</c> if they cancelled.
    /// </summary>
    /// <param name="pickerTitle">Prompt shown on the picker.</param>
    Task<string?> PickJsonTextAsync(string pickerTitle, CancellationToken cancellationToken = default);

    /// <summary>Hands <paramref name="json"/> to the share sheet as <paramref name="fileName"/>.</summary>
    Task ShareJsonAsync(string fileName, string json, string title, CancellationToken cancellationToken = default);
}
