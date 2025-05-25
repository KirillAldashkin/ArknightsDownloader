using ArknightsDownloader.Data;

namespace ArknightsDownloader.Actions;

class CleanupAction : IAction
{
    public string Display => "Delete old versions";

    // populated in 'Run'
    Paths _paths = null!;
    (string Version, string Server)[] _versions = null!;

    public async ValueTask Run(Parameters param, CancellationToken cancel)
    {
        _paths = new Paths(param.Config);
        if (cancel.IsCancellationRequested) return;
        _versions = [.. Directory
            .EnumerateDirectories(_paths.AssetsRoot)
            .SelectMany(server =>
            {
                var name = Path.GetFileName(server)!;
                return Directory
                    .EnumerateDirectories(server)
                    .Select(version => (Version: Path.GetFileName(version), name))
                    .Where(pair => File.Exists(Path.Combine(server, pair.Version, _paths.AssetsDownloaded)));
            })
            .OrderBy(static pair => pair.Version)
            .Prepend((null!, null!))];

        var index = ConsolePal.SelectOne("What to delete?", _versions,
            info => info.Version is null ? "All versions except the newest one for each server" : $"{info.Version} [{info.Server}]");
 
        if (index == -1) return;
        if (index == 0)
        {
            var keptServers = new HashSet<string>();
            for (var i = _versions.Length - 1; i > 0; --i)
            {
                if (keptServers.Add(_versions[i].Server)) continue;
                DeleteVersion(_versions[i].Server, _versions[i].Version);
                if (cancel.IsCancellationRequested) return;
            }
            return;
        }
        DeleteVersion(_versions[index].Server, _versions[index].Version);
    }

    private void DeleteVersion(string server, string version)
    {
        ConsolePal.WriteLine($"Deleting {version} [{server}]...", ConsoleColor.DarkBlue);

        var folder = Path.Combine(_paths.AssetsRoot, server, version);
        HotUpdateList? files;
        using (var file = File.OpenRead(Path.Combine(folder, "hot_update_list.json")))
            if (!Util.TryDeserialize(file, "file set", out files)) return;
        
        foreach(var ab in files.ABInfos)
        {
            var path = Path.GetFullPath(Path.Combine(folder, _paths.AssetsFolder, ab.Name));  // GetFullPath = normalize

            // symlink to something else - safe to delete
            if (File.ResolveLinkTarget(path, true) is not null)
            {
                File.Delete(path);
                continue;
            }

            // This is an actual file, need to check possible symlinks to it
            string? movedTo = null;
            for (var i = 1; i < _versions.Length; ++i)
            {
                var otherFolder = Path.Combine(_paths.AssetsRoot, _versions[i].Server, _versions[i].Version);
                if (!Directory.Exists(otherFolder)) continue;  // could've been deleted in previous 'DeleteVersion' call
                
                // check if there is a link that points to us
                var possibleLink = Path.Combine(otherFolder, _paths.AssetsFolder, ab.Name);
                if (!File.Exists(possibleLink)) continue;
                if (File.ResolveLinkTarget(possibleLink, true) is not FileInfo possibleOriginal) continue;
                if (possibleOriginal.FullName != path) continue;

                // found a link poiting to a file that is to be deleted
                File.Delete(possibleLink);
                if (movedTo is null)
                    File.Move(path, movedTo = possibleLink);
                else
                    File.CreateSymbolicLink(possibleLink, movedTo);
            }
            if (movedTo is null) File.Delete(path);
        }
        Util.EraseDirectory(folder);
    }
}
