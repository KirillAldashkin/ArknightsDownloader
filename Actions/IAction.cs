using ArknightsDownloader.Data;

namespace ArknightsDownloader.Actions;

interface IAction
{
    string Display { get; }

    ValueTask Run(Parameters param, CancellationToken cancellation);
}