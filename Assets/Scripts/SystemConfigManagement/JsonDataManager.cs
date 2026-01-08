using System.IO;

public static class JsonDataManager
{
    private static readonly IJsonSerializer serializer =
        new NewtonsoftJsonSerializer();

    public static void Save<T>(string fileName, T data)
    {
        string path = JsonFilePath.GetPath(fileName);
        string json = serializer.Serialize(data);
        FileIOUtility.WriteText(path, json);
    }

    public static bool Load<T>(string fileName, out T data)
    {
        string path = JsonFilePath.GetPath(fileName);

        if (!File.Exists(path))
        {
            data = default;
            return false;
        }

        string json = FileIOUtility.ReadText(path);
        data = serializer.Deserialize<T>(json);
        return true;
    }

    public static bool Exists(string fileName)
    {
        return File.Exists(JsonFilePath.GetPath(fileName));
    }

    public static void Delete(string fileName)
    {
        string path = JsonFilePath.GetPath(fileName);
        if (File.Exists(path))
            File.Delete(path);
    }
}
