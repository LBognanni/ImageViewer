using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImageViewer;

public interface ITempData
{
    void Write<T>(string what, T value);
    T? Read<T>(string what);
}

public class FileBasedTempData : ITempData
{
    private string FileName => Path.Combine(Path.GetTempPath(), "codemade.imageviewer.tmp");

    internal void EnsureEmpty()
    {
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }
    }
    
    public void Write<T>(string what, T value)
    {
        JObject data = new JObject();
        if (File.Exists(FileName))
        {
            var contents = File.ReadAllText(FileName);
            data = JObject.Parse(contents);
        }

        if (IsStraightValue<T>())
        {
            data[what] = JObject.FromObject(new ValueWrapper<T>(value));
        }
        else
        {
            data[what] = JObject.FromObject(value);
        }

        File.WriteAllText(FileName, data.ToString());
    }

    private static bool IsStraightValue<T>() => !typeof(T).IsClass || typeof(T) == typeof(string);

    public T? Read<T>(string what)
    {
        if (!File.Exists(FileName))
            return default;

        var json = File.ReadAllText(FileName);
        var o = JObject.Parse(json);
        if (o[what] == null)
            return default;


        if (IsStraightValue<T>())
        {
            return (o[what].ToObject<ValueWrapper<T>>()).Value;
        }
        else
        {
            return o[what].ToObject<T>();
        }
    }

    class ValueWrapper<T>
    {
        public T Value { get; set; }

        public ValueWrapper(T data)
        {
            Value = data;
        }
    }
}