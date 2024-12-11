using System;
using System.IO;
using System.Reflection;
using TheArchive.Utilities;

namespace TheArchive.Core.ModulesAPI;

public class CustomSettings<T> : ICustomSetting where T : new()
{
    internal string FullPath { get; private set; }

    internal string FileName { get; private set; }

    public T Value { get; set; }

    private Action<T> AfterLoad { get; set; }

    public bool SaveOnQuit { get; private set; }

    private LoadingTime LoadingTime;

    LoadingTime ICustomSetting.LoadingTime => LoadingTime;

    public bool IsLoaded { get; private set; }

    public CustomSettings(string path, T defaultValue, Action<T> afterLoad = null, LoadingTime loadingTime = LoadingTime.Immediately, bool saveOnQuit = true)
    {
        FileName = path;
        FullPath = Path.Combine(Path.GetDirectoryName(Assembly.GetCallingAssembly().Location), "Settings", $"{FileName}.json");
        Value = defaultValue;
        AfterLoad = afterLoad;
        SaveOnQuit = saveOnQuit;
        LoadingTime = loadingTime;
        CustomSettingsManager.RegisterModuleSetting(this);
    }

    public void Load()
    {
        var root = Path.GetDirectoryName(FullPath);
        if (!Directory.Exists(root)) Directory.CreateDirectory(root);
        if (File.Exists(FullPath))
        {
            Value = JsonConvert.DeserializeObject<T>(File.ReadAllText(FullPath), ArchiveMod.JsonSerializerSettings);
        }
        AfterLoad?.Invoke(Value);

        IsLoaded = true;
    }

    public void Save(bool isQuit = false)
    {
        if (!IsLoaded && isQuit)
            return;
        var root = Path.GetDirectoryName(FullPath);
        if (!Directory.Exists(root)) Directory.CreateDirectory(root);
        string json = string.Empty;
        if (File.Exists(FullPath))
            json = File.ReadAllText(FullPath);
        var rjson = JsonConvert.SerializeObject(Value, ArchiveMod.JsonSerializerSettings);
        if (json.HashString() != rjson.HashString())
            File.WriteAllText(FullPath, rjson);
    }
}

public interface ICustomSetting
{
    void Load();
    void Save(bool isQuit);
    LoadingTime LoadingTime { get; }
    bool SaveOnQuit { get; }
}

public enum LoadingTime
{
    None,
    Immediately,
    AfterGameDataInited
}