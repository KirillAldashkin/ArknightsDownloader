using System.Text.Json.Serialization;

namespace ArknightsDownloader.Data;

record ResourceVersion(
    [property: JsonPropertyName("resVersion")] string Resource,
    [property: JsonPropertyName("clientVersion")] string Client);