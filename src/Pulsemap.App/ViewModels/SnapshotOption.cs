using Pulsemap.App.Core.Models;

namespace Pulsemap.App.ViewModels;

/// <summary>One side of a snapshot comparison picker: either "Current" (<see cref="SnapshotId"/>
/// null) or a saved <see cref="SurveySnapshot"/>, paired with whichever <see cref="Floor"/> list
/// that state actually had — so inter-floor coverage math stays self-consistent within one side of
/// the comparison rather than mixing a snapshot's frozen floors with the survey's live ones.</summary>
public sealed record SnapshotOption(Guid? SnapshotId, string Label, IReadOnlyList<Floor> Floors);
