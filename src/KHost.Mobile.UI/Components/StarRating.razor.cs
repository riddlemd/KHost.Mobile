namespace KHost.Mobile.UI.Components;

public sealed partial class StarRating
{
    [Parameter] public int Value { get; set; }
    [Parameter] public EventCallback<int> ValueChanged { get; set; }
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>When true, a 0 rating is "sung but unrated" — suppresses the "unsung" overlay.</summary>
    [Parameter] public bool Sung { get; set; }

    /// <summary>Colour of the filled stars: "hot" (red — how it went), "cool" (blue — enjoyment), or null (gold).</summary>
    [Parameter] public string? Tone { get; set; }

    private string ToneClass => Tone switch { "hot" => "stars--hot", "cool" => "stars--cool", _ => "" };

    // Tapping the already-selected rank clears back to 0 — the only way to un-rate.
    private async Task SetAsync(int value)
    {
        if (ReadOnly)
            return;

        var next = value == Value ? 0 : value;
        Value = next;
        await ValueChanged.InvokeAsync(next);
    }

    private static string Label(int value) => value switch
    {
        1 => "Not confident",
        2 => "Shaky",
        3 => "Okay",
        4 => "Confident",
        5 => "Nailed it",
        _ => "Unsung",
    };
}
