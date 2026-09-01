using Microsoft.Windows.ApplicationModel.Resources;

namespace Pulsemap.App.Services;

public sealed class LocalizationService : ILocalizationService
{
    private readonly ResourceMap _resourceMap;

    // A pure .NET read (CultureInfo), not a WinRT resource-context query — deliberately avoids
    // touching Windows.ApplicationModel.Resources.Core.ResourceContext any further than the
    // ResourceManager/ResourceMap construction below already needs to, given what SetGlobalQualifierValue
    // turned out to do. Tracks the OS display language in virtually all cases since that's what
    // ultimately drives both.
    public string CurrentLanguage => System.Globalization.CultureInfo.CurrentUICulture.Name;

    public LocalizationService()
    {
        var resourceManager = new ResourceManager("Pulsemap.App.pri");
        _resourceMap = resourceManager.MainResourceMap.GetSubtree("Resources");
    }

    public string GetString(string key) => _resourceMap.GetValue(key).ValueAsString;
}
