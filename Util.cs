using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.CompilerServices;

namespace ArknightsDownloader;

static class Util
{
    public static void CreateFileLink(string link, string target)
    {
        var parent = Path.GetDirectoryName(link);
        if (parent is not null) Directory.CreateDirectory(parent);
        File.CreateSymbolicLink(link, target);
    }

    public static FileStream CreateFile(string path)
    {
        var parent = Path.GetDirectoryName(path);
        if (parent is not null) Directory.CreateDirectory(parent);
        return File.Create(path);
    }

    public static void EraseDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        Directory.Delete(path, true);
    }

    public static Func<long, Stream> WriteSeeker(string path) => pos =>
    {
        var file = File.Exists(path) ? File.OpenWrite(path) : CreateFile(path);
        file.Position = pos;
        return file;
    };

    public static async ValueTask<bool> CheckSuccessCode(this HttpResponseMessage response, string name,
        [CallerFilePath] string file = null!,
        [CallerLineNumber] int line = default)
    {
        if (response.IsSuccessStatusCode) return true;
        var body = await response.Content.ReadAsStringAsync();
        var code = (int)response.StatusCode;
        ConsolePal.WriteError($"Could not fetch {name}: server returned HTTP {code} with message \"{body}\"", file, line);
        return false;
    }

    public static bool TryDeserialize<T>(Stream stream, string name, [NotNullWhen(true)] out T? result,
        [CallerFilePath] string file = null!,
        [CallerLineNumber] int line = default) where T : class
    {
        try
        {
            result = JsonContext.Deserialize<T>(stream);
            if (result is null) throw new($"{name} contains \"null\".");
        }
        catch (Exception e)
        {
            ConsolePal.WriteError($"Could not decode {name}: {e}", file, line);
            result = null;
            return false;
        }
        return true;
    }

    public static bool TryDeserialize<T>(string @string, string name, [NotNullWhen(true)] out T? result,
        [CallerFilePath] string file = null!,
        [CallerLineNumber] int line = default) where T : class
    {
        try
        {
            result = JsonContext.Deserialize<T>(@string);
            if (result is null) throw new($"{name} contains \"null\".");
        }
        catch (Exception e)
        {
            ConsolePal.WriteError($"Could not decode {name}: {e}", file, line);
            result = null;
            return false;
        }
        return true;
    }

    public static string SizeString(double size)
    {
        if (size < 1000) return $"{size}B";
        size /= 1024;
        if (size < 1000) return $"{size:F2}KB";
        size /= 1024;
        if (size < 1000) return $"{size:F2}MB";
        size /= 1024;
        return $"{size:F2}GB";
    }

    public static void Deconstruct<TKey, TElement>(this IGrouping<TKey, TElement> group, out TKey key, out IEnumerable<TElement> elements)
        => (key, elements) = (group.Key, group);


    public static HttpClient MakeHttpClient()
    {
        var handler = new SocketsHttpHandler()
        {
            MaxConnectionsPerServer = ushort.MaxValue,
            EnableMultipleHttp2Connections = true,
            InitialHttp2StreamWindowSize = 16777216
        };
        return new(handler)
        {
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
    }
}