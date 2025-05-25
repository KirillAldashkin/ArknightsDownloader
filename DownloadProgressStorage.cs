using KiDev.StreamCopy;
using System.Buffers.Binary;

namespace ArknightsDownloader;

public class DownloadProgressStorage : IPositionStorage
{
    private string _path;

    private bool _downloaded, _unpacked;
    private long _position, _length;

    public DownloadProgressStorage(string path)
    {
        _path = path;
        if (File.Exists(path))
        {
            var data = File.ReadAllBytes(_path);
            _position = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan()[0..8]);
            _length = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan()[8..16]);
            _downloaded = data[16] != 0;
            _unpacked = data[17] != 0;
        }
        else
        {
            _downloaded = false;
            _unpacked = false;
            _position = 0;
            _length = -1;
        }
    }

    public bool Unpacked
    {
        get => _unpacked;
        set
        {
            if (_unpacked == value) return;
            _unpacked = value;
            UpdateFile();
        }
    }

    public bool Done
    {
        get => _downloaded;
        set
        {
            if(_downloaded == value) return;
            _downloaded = value;
            UpdateFile();
        }
    }

    public long Position
    { 
        get => _position;
        set
        {
            if (_position == value) return;
            _position = value;
            UpdateFile();
        }
    }

    public long Length
    {
        get => _length;
        set
        {
            if (_length == value) return;
            _length = value;
            UpdateFile();
        }
    }

    private void UpdateFile()
    {
        Span<byte> data = stackalloc byte[18];
        BinaryPrimitives.WriteInt64LittleEndian(data[0..8], _position);
        BinaryPrimitives.WriteInt64LittleEndian(data[8..16], _length);
        data[16] = (byte)(_downloaded ? 0xFF : 0x00);
        data[17] = (byte)(_unpacked ? 0xFF : 0x00);

        var parent = Path.GetDirectoryName(_path);
        if (parent is not null) Directory.CreateDirectory(parent);
        File.WriteAllBytes(_path, data);
    }
}