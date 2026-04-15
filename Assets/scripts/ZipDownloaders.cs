using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

[System.CLSCompliant(false)]
public class ZipDownloaders : MonoBehaviour
{
    /// <summary>
    /// Downloads a ZIP file from a URL and saves it locally.
    /// </summary>
    /// <param name="url">Direct download link</param>
    /// <param name="onSuccess">Returns local file path</param>
    /// <param name="onError">Returns error message</param>
    /// <param name="progress">Optional progress callback (0 → 1)</param>
    public IEnumerator DownloadZip(
        string url,
        Action<string> onSuccess,
        Action<string> onError = null,
        Action<float> progress = null)
    {
        string fileName = GetFileNameFromUrl(url);
        string savePath = Path.Combine(Application.persistentDataPath, fileName);

        Debug.Log($"Downloading ZIP to: {savePath}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.downloadHandler = new DownloadHandlerBuffer();

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

            try
            {
                File.WriteAllBytes(savePath, request.downloadHandler.data);
                Debug.Log("Download complete!");

                onSuccess?.Invoke(savePath);
            }
            catch (Exception e)
            {
                Debug.LogError("File write failed: " + e.Message);
                onError?.Invoke(e.Message);
            }
        }
    }

    /// <summary>
    /// Extract filename from URL safely
    /// </summary>
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