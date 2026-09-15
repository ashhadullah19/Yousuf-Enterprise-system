namespace Yousuf_Enterprise_system.ViewModels;

// Drives the shared Excel import popup; posts to the current controller's Import action and
// links to its DownloadTemplate action.
public class ImportModalViewModel
{
    public string Id { get; init; } = "importModal";
    public string Title { get; init; } = string.Empty;
    public string EntityPlural { get; init; } = string.Empty;
    public string RequiredColumn { get; init; } = string.Empty;
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
}
