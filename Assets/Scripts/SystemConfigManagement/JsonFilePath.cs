using System.IO;
using UnityEngine;

public static class JsonFilePath
{
    private static readonly string RootFolder =
        Path.Combine(Application.persistentDataPath, "JsonData");

    public static string GetPath(string fileName)
    {
        if (!Directory.Exists(RootFolder))
            Directory.CreateDirectory(RootFolder);

        return Path.Combine(RootFolder, $"{fileName}.json");
    }
}
