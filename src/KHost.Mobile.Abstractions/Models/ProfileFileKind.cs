namespace KHost.Mobile.Abstractions.Models;

/// <summary>What an imported JSON file turned out to be.</summary>
public enum ProfileFileKind
{
    /// <summary>A <see cref="SingerProfile"/> object (identity + songs + history).</summary>
    Profile,


    /// <summary>Not JSON, or not a shape we recognise.</summary>
    Invalid,
}
