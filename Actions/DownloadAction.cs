using ArknightsDownloader.Data;
using KiDev.StreamCopy;
using System.Collections.Frozen;
using System.IO.Compression;

namespace ArknightsDownloader.Actions;

class DownloadAction : IAction
{
    public string Display => "Download latest version of resources";

    // populated in 'Run'
    AssetManager _assets = null!;
    Paths _paths = null!;
    string _assetRootFolder = null!;
    // populated in 'FetchLinks'
    NetworkConfigLinks _links = null!;
    ResourceVersion _version = null!;
    string _assetRootUrl = null!;
    // populated in 'FetchUpdateLists'
    FrozenDictionary<string, ABInfo> _bundles = null!;
    FrozenDictionary<string, PackInfo> _packs = null!;

    public async ValueTask Run(Parameters param, CancellationToken cancel)
    {
        var server = param.GetServer();
        if (server is null) return;

        using var http = Util.MakeHttpClient();

        _paths = new Paths(param.Config);
        _assets = new AssetManager(_paths);
        if (cancel.IsCancellationRequested) return;
        if (!await FetchLinks(http, server, param.Config, cancel)) return;

        if (_assets.LookupFiles(server.Key, _version.Resource, out _, out _))
        {
            Console.WriteLine("This version is already downloaded.");
            return;
        }
        if (cancel.IsCancellationRequested) return;
        _assetRootFolder = Path.Combine(_paths.AssetsRoot, server.Key, _version.Resource);
        Directory.CreateDirectory(_assetRootFolder);

        if (!await FetchUpdateLists(http, cancel)) return;
        if (cancel.IsCancellationRequested) return;

        var symLinkMarkPath = Path.Combine(_assetRootFolder, _paths.AssetsDoneSymlinks);
        if (!File.Exists(symLinkMarkPath))
        {
            Console.WriteLine("Resolving files from other versions...");
            if (!ResolveFileLinks(cancel)) return;
            Util.CreateFile(symLinkMarkPath).Dispose();
            if (cancel.IsCancellationRequested) return;
        }

        Console.WriteLine("Building download list...");
        var toDownload = FrozenDictionary.ToFrozenDictionary(_bundles.Where(a => !_assets.FindSame(a.Value.Name, a.Value.Hash, out _, out _)));
        var downloader = new Downloader(param.Config.Threads, _assetRootFolder, _assetRootUrl, _paths, toDownload.ContainsKey, cancel);
        foreach(var (pack, bundles) in toDownload.Values.GroupBy(ab => ab.Pid ?? string.Empty))
        {
            var packWeight = (pack == "") ? long.MaxValue : (_packs[pack].TotalSize + param.Config.LPackPreference);
            var ABsWeight = bundles.Sum(ab => ab.TotalSize + param.Config.LPackPreference);
            if (packWeight > ABsWeight)
                foreach (var bundle in bundles)
                    downloader.AddTask(bundle.Name, bundle.TotalSize);
            else
                downloader.AddTask(pack, _packs[pack].TotalSize);
        }
        if (cancel.IsCancellationRequested) return;

        if (downloader.Done) // if not added anything
            ConsolePal.WriteLine("No new files to download", ConsoleColor.DarkYellow);
        else
        {
            var downloading = downloader.Run();
            ThreadPool.QueueUserWorkItem(downloader =>
            {
                var spacer = new ConsolePal.LineSpacing();
                while (!downloader.Done)
                {
                    downloader.DumpProgress(in spacer);
                    Thread.Yield();
                }
                downloader.DumpProgress(in spacer);
            }, downloader, true);
            await downloading;
            if (cancel.IsCancellationRequested) return;
        }

        Util.EraseDirectory(Path.Combine(_assetRootFolder, _paths.AssetsProgress));
        Util.EraseDirectory(Path.Combine(_assetRootFolder, _paths.AssetsTemporary));
        Util.CreateFile(Path.Combine(_assetRootFolder, _paths.AssetsDownloaded)).Dispose();
    }

    private bool ResolveFileLinks(CancellationToken cancel)
    {
        var resDir = Path.Combine(_assetRootFolder, _paths.AssetsFolder);
        Util.EraseDirectory(resDir); // delete garbage from previous unfinished attempt

        var linkCount = 0;
        var linkSize = 0L;
        foreach (var ab in _bundles.Values)
        {
            if (cancel.IsCancellationRequested) return false;
            if (!_assets.FindSame(ab.Name, ab.Hash, out var server, out var version)) continue;

            Util.CreateFileLink(
                Path.Combine(resDir, ab.Name),
                Path.Combine(_paths.AssetsRoot, server, version, _paths.AssetsFolder, ab.Name));
            linkCount++;
            linkSize += ab.TotalSize;
        }
        Console.WriteLine($"Created {linkCount} links, saving {Util.SizeString(linkSize)} of disk space");
        return true;
    }

    private async ValueTask<bool> FetchUpdateLists(HttpClient http, CancellationToken cancel)
    {
        var listsPath = Path.Combine(_assetRootFolder, _paths.UpdateList);
        if (!File.Exists(listsPath))
        {
            Console.WriteLine("Fetching 'hot_update_list.json'...");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_assetRootUrl}/hot_update_list.json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);
            if (cancel.IsCancellationRequested) return false;
            await response.CheckSuccessCode("hot_update_list.json");

            using var newFile = File.Create(listsPath);
            await response.Content.CopyToAsync(newFile, CancellationToken.None);
            if (cancel.IsCancellationRequested) return false;
        }

        using var file = File.OpenRead(listsPath);
        if (!Util.TryDeserialize<HotUpdateList>(file, "hot_update_list.json", out var lists)) return false;

        Console.WriteLine("Building AB lookup...");
        _bundles = FrozenDictionary.ToFrozenDictionary(lists.ABInfos.Select(ab => new KeyValuePair<string, ABInfo>(ab.Name, ab)));
        Console.WriteLine("Building 'lpack' lookup...");
        _packs = FrozenDictionary.ToFrozenDictionary(lists.PackInfos.Select(pack => new KeyValuePair<string, PackInfo>(pack.Name, pack)));
        var totalSize = Util.SizeString(_bundles.Values.Sum(ab => ab.TotalSize));
        Console.WriteLine($"Found {_bundles.Count} bundles with a total size of {totalSize} and {_packs.Count} packs");
        return true;
    }

    private async ValueTask<bool> FetchLinks(HttpClient http, ServerEntry server, Config config, CancellationToken cancel)
    {
        using var configRequest = new HttpRequestMessage(HttpMethod.Get, server.Url);
        Console.WriteLine("Fetching 'network_config'...");
        using var configResponse = await http.SendAsync(configRequest, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);
        if (cancel.IsCancellationRequested) return false;
        await configResponse.CheckSuccessCode("network_config");

        var netStream = configResponse.Content.ReadAsStream(CancellationToken.None);
        if (!Util.TryDeserialize<NetworkConfig>(netStream, "network_config", out var netConfig)) return false;
        if (cancel.IsCancellationRequested) return false;

        if (!Util.TryDeserialize<NetworkConfigContent>(netConfig.Content, "network_config.content", out var content)) return false;
        if(cancel.IsCancellationRequested) return false;

        var links = content.Configs[content.FuncVer].Network;
        if (links is null)
        {
            ConsolePal.WriteError("Current version in 'network_config.content' does not have any links");
            return false;
        }
        _links = links;

        Console.WriteLine("Fetching resource version...");
        var resVerLink = _links.ResourceVersion.Replace("{0}", config.Platform);
        using var resVerRequest = new HttpRequestMessage(HttpMethod.Get, resVerLink);
        using var resVerResponse = await http.SendAsync(resVerRequest, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);
        if (cancel.IsCancellationRequested) return false;
        await resVerResponse.CheckSuccessCode("resource version");

        var resVerStream = resVerResponse.Content.ReadAsStream(CancellationToken.None);
        if (!Util.TryDeserialize<ResourceVersion>(resVerStream, "resrouce version", out var resVer)) return false;
        if (cancel.IsCancellationRequested) return false;
        _version = resVer;

        Console.WriteLine($"Current resource version is \"{resVer.Resource}\"");
        _assetRootUrl = $"{_links.Resources}/{config.Platform}/assets/{_version.Resource}";

        return true;
    }
}

class Downloader(int threads, string rootFolder, string rootUrl, Paths paths, Func<string, bool> unpackPredicate, CancellationToken cancel)
{
    private Lock _setsLock = new();
    private SortedSet<WorkItem> _pending = [];
    private WorkItem?[] _executing = new WorkItem[threads];
    private SortedSet<WorkItem> _finished = [];
    private long _pendingSize = 0;
    private long _finishedSize = 0;

    private long _totalSize = 0;
    private int _totalItems = 0;
    private int _finishedItems = 0;

    public bool Done => _finishedItems == _totalItems;

    public void AddTask(string name, long size)
    {
        var progressPath = Path.Combine(rootFolder, paths.AssetsProgress, name);
        var progress = new DownloadProgressStorage(progressPath);
        _pending.Add(new(progress, name, size));
        _totalSize += size;
        _pendingSize += size;
        _totalItems++;
    }

    public void DumpProgress(in ConsolePal.LineSpacing lines)
    {
        Console.CursorVisible = false;
        var liner = lines.Start();
        lock (_setsLock)
        {
            var total = Util.SizeString(_totalSize);
            var pending = Util.SizeString(_pendingSize);
            var finished = Util.SizeString(_finishedSize);
            var pendingPercent = (float)(_pendingSize * 1000 / _totalSize) / 10;
            var finishedPercent = (float)(_finishedSize * 1000 / _totalSize) / 10;
            // pending - common
            ConsolePal.Write($"{_pending.Count}/{_totalItems} pending: {pending}/{total} ({pendingPercent:F1}%)", ConsoleColor.DarkCyan);
            liner.NextLine();
            // in progress - per thread
            for(int i = 0; i < threads; ++i)
            {
                var item = _executing[i];
                if (item is null) continue;
                ConsolePal.Write($"* '{item.Name}': ", ConsoleColor.White);
                if (!item.Progress.Done && item.Progress.Position == 0)
                    ConsolePal.Write("Waiting...", ConsoleColor.White);
                else if (!item.Progress.Done)
                {
                    var haveSize = Util.SizeString(item.Progress.Position);
                    var needSize = Util.SizeString(item.Size);
                    var percent = (float)(item.Progress.Position * 1000 / item.Size) / 10;
                    ConsolePal.Write($"Downloading {haveSize}/{needSize} ({percent:F1}%)", ConsoleColor.DarkYellow);
                }
                else if (!item.Progress.Unpacked)
                    ConsolePal.Write("Unpacking...", ConsoleColor.DarkMagenta);
                else
                    ConsolePal.Write("Done", ConsoleColor.White);
                liner.NextLine();
            }
            // done - common
            ConsolePal.Write($"{_finished.Count}/{_totalItems} finished: {finished}/{total} ({finishedPercent:F1}%)", ConsoleColor.DarkGreen);
            liner.NextLine();
        }
        liner.Finish();
        Console.CursorVisible = true;
    }

    public async ValueTask Run()
    {
        var tasks = new Task[threads];
        for (var i = 0; i < threads; i++)
        {
            var ii = i;
            tasks[i] = Task.Run(() => Worker(ii));
        }
        await Task.WhenAll(tasks);
        _finishedItems = _totalItems; // override that in case of cancellation
    }

    private void Worker(int index)
    {
        using var http = Util.MakeHttpClient();
        while(true)
        {
            // enter
            lock (_setsLock)
            {
                if (_pending.Count == 0) break;
                _executing[index] = _pending.Min;
                _pending.Remove(_executing[index]!);
                _pendingSize -= _executing[index]!.Size;
            }
            if (cancel.IsCancellationRequested) break;
            var item = _executing[index]!;

            var tempPath = Path.Combine(rootFolder, paths.AssetsTemporary, item.Name);

            if (!item.Progress.Done)
            {
                var url = $"{rootUrl}/{Path.ChangeExtension(item.Name.Replace('/', '_').Replace("#", "__"), "dat")}";
                HttpRequestMessage request() => new(HttpMethod.Get, url);
                HttpUtils.Download(http, request, Util.WriteSeeker(tempPath), item.Progress, null, cancel);
            }
            if (cancel.IsCancellationRequested) break;

            if (!item.Progress.Unpacked)
            {
                var resDir = Path.Combine(rootFolder, paths.AssetsFolder);
                using (var zip = ZipFile.OpenRead(tempPath))
                    foreach(var entry in zip.Entries)
                    {
                        if (!unpackPredicate(entry.FullName)) continue;
                        var target = Path.Combine(resDir, entry.FullName);
                        Util.CreateFile(target).Dispose();
                        entry.ExtractToFile(target, true);
                        if (cancel.IsCancellationRequested) break;
                    }
                File.Delete(tempPath);
                item.Progress.Unpacked = true;
            }
            if (cancel.IsCancellationRequested) break;

            // exit
            Interlocked.Increment(ref _finishedItems);
            lock (_setsLock)
            {
                _finishedSize += _executing[index]!.Size;
                _finished.Add(_executing[index]!);
                _executing[index] = null;
            }
            if (cancel.IsCancellationRequested) break;
        }
    }

    record WorkItem(DownloadProgressStorage Progress, string Name, long Size) : IComparable<WorkItem>
    {
        public int CompareTo(WorkItem? other) => Name.CompareTo(other?.Name);
    }
}