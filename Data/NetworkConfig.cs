using System.Text.Json.Serialization;

namespace ArknightsDownloader.Data;

public record NetworkConfig(
    string Sign,
    string Content);

public record NetworkConfigContent(
    string FuncVer,
    string ConfigVer,
    IReadOnlyDictionary<string, NetworkConfigVersion> Configs);

public record NetworkConfigVersion(
    bool Override,
    NetworkConfigLinks? Network);

public record NetworkConfigLinks(
    [property: JsonPropertyName("hu")] string Resources,
    [property: JsonPropertyName("hv")] string ResourceVersion);