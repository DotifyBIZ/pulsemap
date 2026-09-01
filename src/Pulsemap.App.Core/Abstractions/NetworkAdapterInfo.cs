namespace Pulsemap.App.Core.Abstractions;

/// <summary>A wireless network adapter available on this machine.</summary>
public sealed record NetworkAdapterInfo(Guid Id, string Name);
