using Bruinet.Presenter.Models;

namespace Bruinet.Presenter.Services;

public interface IBddWorkspaceService
{
    string? FindDefaultWorkspace();

    BddWorkspaceLoadResult LoadFolder(string folderPath);
}
