using ArknightsDownloader.Data;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace ArknightsDownloader;

using FileTree = (string Root, FrozenSet<string> Files);
using TextSpan = ReadOnlySpan<char>;

class AssetManager
{
    // server => version => (root, files)
    FrozenDictionary<string, FrozenDictionary<string, FileTree>.AlternateLookup<TextSpan>>.AlternateLookup<TextSpan> _files;
    // path \0 hash => (server, version)
    FrozenDictionary<string, (string Server, string Version)>.AlternateLookup<TextSpan> _hashes;

    public AssetManager(Paths paths)
    {
        Console.WriteLine("Building file tree...");
        _files = BuildFiles(paths).GetAlternateLookup<TextSpan>();
        Console.WriteLine("Building hash resolver...");
        _hashes = BuildHashes(paths).GetAlternateLookup<TextSpan>();

        var fileCount = 0L;
        foreach (var server in _files.Dictionary.Values)
            foreach (var (_, files) in server.Dictionary.Values)
                fileCount += files.Count;
        Console.WriteLine($"Processed {fileCount} files ({_hashes.Dictionary.Count} unique)");
    }

    public bool FindSame(TextSpan path, TextSpan hash,
        [NotNullWhen(true)] out string? server,
        [NotNullWhen(true)] out string? version)
    {
        Span<char> key = stackalloc char[path.Length + 1 + hash.Length];
        path.CopyTo(key);
        key[path.Length] = '\0';
        hash.CopyTo(key[(path.Length+1)..]);

        var found = _hashes.TryGetValue(key, out var pair);
        server = found ? pair.Server : null;
        version = found ? pair.Version : null;
        return found;
    }

    public bool LookupFiles(TextSpan server, TextSpan version,
        [NotNullWhen(true)] out string? root,
        [NotNullWhen(true)] out FrozenSet<string>? files)
    {
        if (!_files.TryGetValue(server, out var versions))
        {
            root = null;
            files = null;
            return false;
        }
        var found = versions.TryGetValue(version, out var pair);
        root = found ? pair.Root : null;
        files = found ? pair.Files : null;
        return found;
    }

    private FrozenDictionary<string, (string Server, string Version)> BuildHashes(Paths paths) =>
        FrozenDictionary.ToFrozenDictionary(
            EnumerateAssetFolders(paths)
            .SelectMany(p => p.Versions.Select(v => (p.Server, Version: v)))
            .AsParallel()
            .SelectMany(pair =>
            {
                var root = Path.Combine(paths.AssetsRoot, pair.Server, pair.Version);
                using var file = File.OpenRead(Path.Combine(root, paths.UpdateList));
                var list = JsonContext.Deserialize<HotUpdateList>(file)!;
                return list.ABInfos
                    .Where(ab =>
                    {
                        var file = Path.Combine(root, paths.AssetsFolder, ab.Name);
                        return File.ResolveLinkTarget(file, false) is null;
                    })
                    .Select(ab =>
                    {
                        var combined = string.Concat(ab.Name, "\0", ab.Hash);
                        return new KeyValuePair<string, (string, string)>(combined, pair);
                    })
                    .DistinctBy(ab => ab.Key);
            })
        );

    private FrozenDictionary<string, FrozenDictionary<string, FileTree>.AlternateLookup<TextSpan>> BuildFiles(Paths paths) =>
        FrozenDictionary.ToFrozenDictionary(
            EnumerateAssetFolders(paths)
            .Select(s => new KeyValuePair<string, FrozenDictionary<string, FileTree>.AlternateLookup<TextSpan>>(
                s.Server,
                FrozenDictionary.ToFrozenDictionary(
                    s.Versions
                    .Select(version =>
                    {
                        var root = Path.Combine(paths.AssetsRoot, s.Server, version);
                        var resRoot = Path.Combine(root, paths.AssetsFolder);
                        using var file = File.OpenRead(Path.Combine(root, paths.UpdateList));
                        var list = JsonContext.Deserialize<HotUpdateList>(file)!;
                        var files = FrozenSet.ToFrozenSet(list.ABInfos.Select(ab => ab.Name));
                        return new KeyValuePair<string, FileTree>(version, (resRoot, files));
                    })
                ).GetAlternateLookup<TextSpan>()
            ))
        );

    private IEnumerable<(string Server, IEnumerable<string> Versions)> EnumerateAssetFolders(Paths paths)
    {
        Directory.CreateDirectory(paths.AssetsRoot);
        return Directory.EnumerateDirectories(paths.AssetsRoot)
            .Select(server => (
                Path.GetFileName(server),
                Directory.EnumerateDirectories(server)
                    .Where(verPath => File.Exists(Path.Combine(verPath, paths.AssetsDownloaded)))
                    .Select(version => Path.GetFileName(version))
            ))
            .Where(pair => pair.Item2.Any());
    }
}
