using System.IO;
using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class TargetData
{
    public int id;
    public string image;
    public string model;
}

[Serializable]
public class PackageData
{
    public string lesson_id;
    public List<TargetData> targets;
}

public static class JsonLoader
{
    public static PackageData Load(string folderPath)
    {
        // ✅ Correct file name
        string jsonPath = Path.Combine(folderPath, "package.json");

        if (!File.Exists(jsonPath))
        {
            Debug.LogError("package.json not found at: " + jsonPath);
            return null;
        }

        try
        {
            string jsonContent = File.ReadAllText(jsonPath);

            PackageData package = JsonUtility.FromJson<PackageData>(jsonContent);

            // ✅ Validate parsed data
            if (package == null || package.targets == null)
            {
                Debug.LogError("JSON parsing failed or targets missing!");
                return null;
            }

            Debug.Log($"Loaded lesson: {package.lesson_id}");
            Debug.Log($"Loaded {package.targets.Count} targets.");

            return package;
        }
        catch (Exception e)
        {
            Debug.LogError("JSON Load Exception: " + e.Message);
            return null;
        }
    }
}