using Pulsemap.App.Core.Models;

namespace Pulsemap.App.ViewModels;

/// <summary>A <see cref="Band"/> paired with its localized name, so a band picker can bind
/// straight to a list without showing raw enum identifiers.</summary>
public sealed record BandChoice(Band Band, string Label);
