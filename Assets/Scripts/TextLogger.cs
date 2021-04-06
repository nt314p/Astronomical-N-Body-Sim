using System.IO;

public class TextLogger
{
    public static void Log(string message)
    {
        var path = "Assets/Resources/log.txt";
        var writer = new StreamWriter(path, true);
        writer.WriteLine(message);
        writer.Close();
    }
}
