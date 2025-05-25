using ArknightsDownloader.Data;

namespace ArknightsDownloader;

class Paths(Config config)
{
    public string AbsoluteRoot => Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, config.Folder);
    public string AssetsRoot => Path.Combine(AbsoluteRoot, "assets");
    public string AssetsDownloaded => "download_complete";
    public string AssetsDoneSymlinks => "links_resolved";
    public string AssetsFolder => "resources";
    public string AssetsProgress => "progress";
    public string AssetsTemporary => "temporary";
    public string UpdateList => "hot_update_list.json";
}
