namespace ArknightsDownloader.Data;

record HotUpdateList(IReadOnlyList<ABInfo> ABInfos, IReadOnlyList<PackInfo> PackInfos);

record ABInfo(string Name, string Hash, long TotalSize, long ABSize, string? Pid) : IComparable<ABInfo>
{
    public int CompareTo(ABInfo? other) => Name.CompareTo(other?.Name);
}

record PackInfo(string Name, long TotalSize);