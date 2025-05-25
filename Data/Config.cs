
using ArknightsDownloader.Actions;

namespace ArknightsDownloader.Data;

record Config(
    IReadOnlyList<ServerEntry> Servers,
    int Threads,  // amount of download threads
    long LPackPreference,  // how much is downloading single lpack better than downloading separate ABs
    string Platform,  // used when communication with HG server, only tested value is 'android'
    string Folder);  // relative to executable path

record ServerEntry(
    string Key,  // used for file system and CLI arguments
    string Name,  // display name
    string Url);  // 'network_config'

record Parameters(Config Config)
{
    public static Parameters ParseCLI(string[] args, Config config) => new(config);

    public ServerEntry? GetServer()
    {
        var index = ConsolePal.SelectOne("Select a server:", Config.Servers, s => s.Name);
        if (index == -1) return null;

        return Config.Servers[index];
    }

    public int SelectAction(IAction[] actions) => 
        ConsolePal.SelectOne("Select action:", actions, a => a.Display);
};