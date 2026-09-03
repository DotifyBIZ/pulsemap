namespace Pulsemap.App.Services;

/// <param name="PageCount">1 for a non-PDF file. For a PDF, the file's real page count — lets the
/// wizard offer a page picker only when there's more than one page to choose from.</param>
public sealed record FloorPlanFilePickResult(string FileName, string FileExtension, byte[] ImageData, int PageCount = 1);
