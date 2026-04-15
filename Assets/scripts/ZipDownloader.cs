using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[System.CLSCompliant(false)]
public class ZipDownloader : MonoBehaviour
{
    public IEnumerator DownloadZip(
    string url,
    Action<string> onSuccess,
    Action<string> onError = null,
    Action<float> progress = null)
{
    string fileName = GetFileNameFromUrl(url);
    string savePath = Path.Combine(Application.persistentDataPath, fileName);

    Directory.CreateDirectory(Application.persistentDataPath);

    Debug.Log($"Downloading ZIP to: {savePath}");

    using (UnityWebRequest request = UnityWebRequest.Get(url))
    {
        request.downloadHandler = new DownloadHandlerFile(savePath);
        request.timeout = 30;

        var operation = request.SendWebRequest();

        while (!operation.isDone)
        {
            progress?.Invoke(request.downloadProgress);
            yield return null;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Download failed: " + request.error);
            onError?.Invoke(request.error);
            yield break;
        }

        Debug.Log("Download complete!");
        onSuccess?.Invoke(savePath);
    }
}
    private string GetFileNameFromUrl(string url)
    {
        try
        {
            Uri uri = new Uri(url);
            return Path.GetFileName(uri.LocalPath);
        }
        catch
        {
            return "lesson.zip";
        }
    }
}