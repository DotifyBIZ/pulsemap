using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Pulsemap.App.Core.Diagnostics;

namespace Pulsemap.App.Converters;

/// <summary>Maps a <see cref="DiagnosticSeverity"/> to one of the Dotify status text brushes in App.xaml —
/// kept as a converter (rather than a computed property on the ViewModel-layer display record) so
/// that record stays free of any WinUI dependency and constructible in a plain unit test.</summary>
public sealed class DiagnosticSeverityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        string resourceKey = value is DiagnosticSeverity.Error ? "DotifyColorErrorTextBrush" : value is DiagnosticSeverity.Warning ? "DotifyColorWarningTextBrush" : "DotifyColorSuccessTextBrush";
        return (Brush)Application.Current.Resources[resourceKey];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
