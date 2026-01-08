using System.IO;
using System.Text;

public static class FileIOUtility
{
    public static void WriteText(string path, string content)
    {
        File.WriteAllText(path, content, Encoding.UTF8);
    }

    public static string ReadText(string path)
    {
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
