using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ArknightsDownloader.Data;

namespace ArknightsDownloader;

[JsonSerializable(typeof(Config))]
[JsonSerializable(typeof(NetworkConfig))]
[JsonSerializable(typeof(NetworkConfigContent))]
[JsonSerializable(typeof(ResourceVersion))]
[JsonSerializable(typeof(HotUpdateList))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
partial class JsonContext : JsonSerializerContext
{
    public static T? Deserialize<T>(Stream from) where T : class => 
        JsonSerializer.Deserialize(from, (JsonTypeInfo<T>)Default.GetTypeInfo(typeof(T))!);

    public static T? Deserialize<T>(string from) where T : class =>
        JsonSerializer.Deserialize(from, (JsonTypeInfo<T>)Default.GetTypeInfo(typeof(T))!);
}
