using ArknightsDownloader;
using ArknightsDownloader.Actions;
using ArknightsDownloader.Data;

var config = LoadConfig();
if (config is null) return -1;

#if DEBUG
Console.Write("args: ");
args = Console.ReadLine()!.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
#endif
var param = Parameters.ParseCLI(args, config);
if (param is null) return -1;

IAction[] actions = [
    new DownloadAction(),
    new CleanupAction()
];
var index = param.SelectAction(actions);
if (index == -1) return 0;

ConsolePal.WriteLine("Starting action (press Ctrl+C to interrupt, task will be stopped ASAP)...", ConsoleColor.DarkCyan);
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    cts.Cancel();
    e.Cancel = true;
};
await actions[index].Run(param, cts.Token);

return 0;

static Config? LoadConfig()
{
    if (Environment.ProcessPath is not string execPath)
    {
        ConsolePal.WriteError("Could not get executable path");
        return null;
    }
    var path = Path.Combine(Path.GetDirectoryName(execPath)!, "config.json");
    if (!File.Exists(path))
    {
        ConsolePal.WriteError("'config.json' does not exist");
        return null;
    }
    using var file = File.OpenRead(path);
    Config? config = null;
    try
    {
        config = JsonContext.Deserialize<Config>(file);
        if (config is null) throw new("'config.json' contains \"null\".");
    }
    catch (Exception e)
    {
        ConsolePal.WriteError($"Could not decode 'config.json': {e}");
    }
    return config;
}
