using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Pulsemap.App.Services;

/// <summary>Wraps the native file-open picker — unpackaged apps need the owning HWND wired in via InitializeWithWindow before the picker can show.</summary>
public sealed class FloorPlanFilePickerService : IFloorPlanFilePickerService
{
    /// <summary>
    /// Ceiling on a picked floor plan. The bytes are read fully into memory, embedded in the
    /// .pulsemap zip, and (for a PDF) handed to PDFium — an unbounded read of a user-chosen file
    /// is the app's widest untrusted-input boundary. Comfortably above any real architectural
    /// drawing, and below the 200MB per-entry cap ZipSurveyFileService enforces when reading a
    /// survey back, so a file accepted here can always be reopened.
    /// </summary>
    public const long MaxFloorPlanBytes = 100L * 1024 * 1024;

    public async Task<FloorPlanFilePickResult?> PickFloorPlanFileAsync(CancellationToken cancellationToken = default)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, App.WindowHandle);

        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".pdf");

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return null;
        }

        var info = new FileInfo(file.Path);
        if (info.Length > MaxFloorPlanBytes)
        {
            throw new FloorPlanTooLargeException(info.Length, MaxFloorPlanBytes);
        }

        byte[] imageData = await File.ReadAllBytesAsync(file.Path, cancellationToken);
        string extension = Path.GetExtension(file.Path);
        int pageCount = string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase)
            ? GetPdfPageCount(imageData)
            : 1;

        return new FloorPlanFilePickResult(file.Name, extension, imageData, pageCount);
    }

    // A corrupt, password-protected, or otherwise unreadable PDF throws one of PDFtoImage's own
    // exception types here — let it propagate. The wizard's existing catch for
    // IOException/UnauthorizedAccessException around this call already needs to widen to include
    // it, since a bad PDF is exactly the same "couldn't read that file" story to the user.
    private static int GetPdfPageCount(byte[] pdfBytes)
    {
        using var stream = new MemoryStream(pdfBytes);
        return PDFtoImage.Conversion.GetPageCount(stream);
    }
}
