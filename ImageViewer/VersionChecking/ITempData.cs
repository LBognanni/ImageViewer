using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ImageViewer;

public interface ITempData
{
    void WriteTime(string what, DateTime value);
    DateTime? ReadTime(string what);
}

public class FileBasedTempData : ITempData
{
    private readonly string _fileName;

    public FileBasedTempData() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImageViewer", "state.json"))
    {
    }

    public FileBasedTempData(string fileName)
    {
        _fileName = fileName;
    }

    public void WriteTime(string what, DateTime value)
    {
        var data = Load();
        data[what] = value;
        Directory.CreateDirectory(Path.GetDirectoryName(_fileName)!);
        File.WriteAllText(_fileName, JsonSerializer.Serialize(data, AppJsonContext.Default.DictionaryStringDateTime));
    }

    public DateTime? ReadTime(string what) => Load().TryGetValue(what, out var value) ? value : null;

    private Dictionary<string, DateTime> Load()
    {
        try
        {
            if (File.Exists(_fileName))
            {
                return JsonSerializer.Deserialize(File.ReadAllText(_fileName), AppJsonContext.Default.DictionaryStringDateTime) ?? new();
            }
        }
        catch (JsonException)
        {
            // Only small transient values live here, so a damaged file is safe to start over
        }
        return new();
    }
}
