using System.IO;
using System.IO.Compression;
using UnityEngine;

public static class ZipExtractor
{
    public static string Extract(string zipPath)
    {
        string extractPath = Path.Combine(Application.persistentDataPath, "ExtractedContent");

        // Clean up previous extraction if it exists
        if (Directory.Exists(extractPath))
        {
            Directory.Delete(extractPath, true);
        }
        
        Directory.CreateDirectory(extractPath);
        ZipFile.ExtractToDirectory(zipPath, extractPath);
        
        Debug.Log("Extracted to: " + extractPath);

        // Optionally delete the zip file to save space
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        return extractPath;
    }
}
