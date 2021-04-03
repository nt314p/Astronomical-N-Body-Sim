using UnityEngine;
using System.IO;

public class TextLogger : MonoBehaviour
{
    public static void Log(string message)
    {
        string path = "Assets/Resources/log.txt";

        StreamWriter writer = new StreamWriter(path, true);
        writer.WriteLine(message);
        writer.Close();
    }
}
