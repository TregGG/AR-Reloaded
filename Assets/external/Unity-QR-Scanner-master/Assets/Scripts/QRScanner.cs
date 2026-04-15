using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Android;
using ZXing;

public class QRScanner : MonoBehaviour
{
    private WebCamTexture webcamTexture;
    private string QrCode = string.Empty;
    private RawImage rawImage;

    void Start()
    {
        rawImage = GetComponent<RawImage>();
        StartCoroutine(InitializeCameraRoutine());
    }

    IEnumerator InitializeCameraRoutine()
    {
        // 1. Request and WAIT for Permissions
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
            
            // 🔥 Wait until the user actually clicks "Allow"
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                yield return null; 
            }
        }
#elif UNITY_IOS
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) 
            {
                Debug.LogError("Camera permission denied.");
                yield break;
            }
        }
#endif

        // 2. Find the Back Camera
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("No camera found on this device!");
            yield break;
        }

        string backCamName = devices[0].name; // Fallback to first camera
        foreach (var device in devices)
        {
            if (!device.isFrontFacing)
            {
                backCamName = device.name;
                break;
            }
        }

        // 3. Initialize and Play
        webcamTexture = new WebCamTexture(backCamName, 1280, 720);
        webcamTexture.Play();

        // 4. 🔥 Wait for the camera to physically spin up
        // Mobile cameras often take 1-2 seconds to start sending pixel data
        while (!webcamTexture.isPlaying || webcamTexture.width < 100)
        {
            yield return null;
        }

        Debug.Log($"Camera ready: {webcamTexture.width}x{webcamTexture.height}");

        // Assign texture ONLY after the camera is fully ready
        rawImage.texture = webcamTexture;

        // 5. Start Scanning
        StartCoroutine(ScanQRCode());
    }

    IEnumerator ScanQRCode()
    {
        IBarcodeReader reader = new BarcodeReader
        {
            AutoRotate = true,
            TryInverted = true
        };

        while (string.IsNullOrEmpty(QrCode))
        {
            // 🔥 Scan every 0.25 seconds instead of every frame
            // This stops mobile devices from lagging/overheating
            yield return new WaitForSeconds(0.25f); 

            try
            {
                if (webcamTexture != null && webcamTexture.isPlaying && webcamTexture.didUpdateThisFrame)
                {
                    Color32[] pixels = webcamTexture.GetPixels32();
                    Result result = reader.Decode(pixels, webcamTexture.width, webcamTexture.height);

                    if (result != null)
                    {
                        QrCode = result.Text;
                        Debug.Log("QR DETECTED: " + QrCode);
                        
                        webcamTexture.Stop();
                        ContentManager.Instance.StartLoading(QrCode);
                        
                        yield break; // Stop the coroutine once found
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("QR Scan Error: " + e.Message);
            }
        }
    }

    void Update()
    {
        if (webcamTexture == null || !webcamTexture.isPlaying)
            return;

        // 🔥 Handle Mobile Rotation & Mirroring
        // Your logic here was good, but we only need to apply it while playing
        rawImage.rectTransform.localEulerAngles = new Vector3(0, 0, -webcamTexture.videoRotationAngle);
        rawImage.rectTransform.localScale = new Vector3(1, webcamTexture.videoVerticallyMirrored ? -1 : 1, 1);
    }

    void OnDisable()
    {
        // Always clean up the camera when the script is disabled/destroyed
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }
    }

    // Optional debug GUI to see the code on screen
    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 40;
        style.normal.textColor = Color.green;
        GUI.Label(new Rect(10, 10, 1000, 50), "QR: " + QrCode, style);
    }
}