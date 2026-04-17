using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.IO;
using GLTFast;

public class ContentManager : MonoBehaviour
{
    public static ContentManager Instance;
    [SerializeField] XRReferenceImageLibrary baseLibrary;
    public Dictionary<string, GameObject> modelMap = new();

    // Cache for scene transition
    private PackageData cachedPackage;
    private string cachedFolder;
    [SerializeField] GameObject loadingScreen;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // 🔥 CRITICAL FIX: Force this object to the root of the hierarchy
            transform.SetParent(null); 

            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // Destroy(gameObject);
            return;
        }
    }

    public void StartLoading(string url)
    {
        StartCoroutine(LoadPipeline(url));
    }

    IEnumerator LoadPipeline(string url)
    {
        Debug.Log("[Pipeline] Starting...");

        // 1. Download
        string zipPath = null;
        loadingScreen.SetActive(true);
        var downloader = GetComponent<ZipDownloader>();
        if (downloader == null)
        {
            Debug.LogError("ZipDownloader missing!");
            yield break;
        }

        yield return StartCoroutine(
            downloader.DownloadZip(url,
                (path) => zipPath = path,
                (err) => Debug.LogError("Download error: " + err)
            )
        );

        if (string.IsNullOrEmpty(zipPath))
        {
            Debug.LogError("Download failed, zipPath null");
            yield break;
        }

        // 2. Extract
        string folder = ZipExtractor.Extract(zipPath);

        if (string.IsNullOrEmpty(folder))
        {
            Debug.LogError("Extraction failed!");
            yield break;
        }

        // 3. Parse JSON
        var package = JsonLoader.Load(folder);

        if (package == null || package.targets == null)
        {
            Debug.LogError("Invalid package data!");
            yield break;
        }

        // Cache for after scene load
        cachedPackage = package;
        cachedFolder = folder;

        Debug.Log("[Pipeline] Loading ARScene...");

        // 4. Load AR Scene properly (FIXED)
        #if !UNITY_EDITOR
            yield return SceneManager.LoadSceneAsync("ARScene");
            
            Debug.Log("[Pipeline] Waiting for AR hardware to boot...");

            // 🔥 SMART WAIT: Pause the coroutine until the AR subsystem is officially Ready or Tracking
            float timeout = 5f;
            float timer = 0f;

            while (ARSession.state != ARSessionState.SessionTracking && timer < timeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            Debug.Log("AR State after wait: " + ARSession.state);
            // Give it one extra frame just to be completely safe
            yield return null;
        #endif
        Debug.Log("[Pipeline] Started ProcessContent...");
        
        yield return StartCoroutine(ProcessContent(package, folder));

        Debug.Log("[Pipeline] Content Ready!");
    }
    void OnDestroy()
    {
        Debug.LogError($"[DEBUG] ContentManager was destroyed! StackTrace: {StackTraceUtility.ExtractStackTrace()}");
    }
    IEnumerator ProcessContent(PackageData package, string folder)
    {
        Debug.Log("[Process] Starting content injection...");

    #if UNITY_EDITOR
        Debug.Log("[EDITOR MODE] Running TEST scene logic...");

        float spacing = 1.5f;
        int index = 0;

        foreach (var t in package.targets)
        {
            string imagePath = System.IO.Path.Combine(folder, t.image);
            string modelPath = System.IO.Path.Combine(folder, t.model);

            // 🔥 TEST IMAGE LOAD
            Texture2D tex = LoadTexture(imagePath);

            if (tex != null)
            {
                Debug.Log($"[EDITOR] Image loaded: {t.image} | {tex.width}x{tex.height}");
            }
            else
            {
                Debug.LogError($"[EDITOR] Failed to load image: {t.image}");
            }

            // 🔥 LOAD GLB
            var gltf = new GLTFast.GltfImport();
            bool success = false;

            var loadTask = gltf.Load("file://" + modelPath);

            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            success = loadTask.Result;

            if (!success)
            {
                Debug.LogError($"[EDITOR] Failed to load model: {modelPath}");
                continue;
            }

            string name = System.IO.Path.GetFileNameWithoutExtension(t.image).ToLower();

            GameObject parent = new GameObject(name);

            yield return gltf.InstantiateMainSceneAsync(parent.transform);
            parent.transform.localScale = Vector3.one * 0.02f;

            // 🔥 POSITION IN FRONT OF CAMERA
            parent.transform.position =
                Camera.main.transform.position +
                Camera.main.transform.forward * 2f +
                Camera.main.transform.right * (index * spacing);

            // parent.transform.localScale = Vector3.one * 0.5f;

            Debug.Log($"[EDITOR] Spawned model: {name}");

            index++;
        }

        Debug.Log("[EDITOR MODE] Test complete!");

        yield break;
    #endif

        Debug.Log("[Pipeline] Build version Running!");
        // 🔥 REAL AR PIPELINE (DEVICE ONLY)

        var imageManager = FindFirstObjectByType<ARTrackedImageManager>();

        if (imageManager == null)
        {
            Debug.LogError("ARTrackedImageManager not found!");
            yield break;
        }

        imageManager.referenceLibrary = imageManager.CreateRuntimeLibrary(baseLibrary);

        var library = imageManager.referenceLibrary as MutableRuntimeReferenceImageLibrary;

        if (library == null)
        {
            Debug.LogError("Failed to create mutable runtime image library!");
            yield break;
        }

        imageManager.enabled = false;

        foreach (var t in package.targets)
        {
            string imagePath = System.IO.Path.Combine(folder, t.image);
            string modelPath = System.IO.Path.Combine(folder, t.model);

            if (!System.IO.File.Exists(imagePath) || !System.IO.File.Exists(modelPath))
            {
                Debug.LogWarning($"Skipping invalid target ID {t.id}");
                continue;
            }

            Texture2D tex = LoadTexture(imagePath);

            if (tex == null)
                continue;

            string imageName = System.IO.Path.GetFileNameWithoutExtension(imagePath).ToLower();

            // 🔥 1. Schedule the background processing job
            var jobState = library.ScheduleAddImageWithValidationJob(tex, imageName, 0.1f);

            // 🔥 2. Actually wait for the CPU to finish crunching the image data
            while (!jobState.jobHandle.IsCompleted)
            {
                yield return null;
            }

            // 🔥 3. Verify it was successful before continuing
            if (jobState.status != AddReferenceImageJobStatus.Success)
            {
                Debug.LogError($"[Process] FAILED to add image {imageName}. Status: {jobState.status}");
                continue; 
            }

            Debug.Log($"[Process] Added image successfully: {imageName}");

            LoadGLBModel(modelPath, imageName);
            
            Debug.Log($"[Process] Loading model async: {imageName}");
        }

        imageManager.enabled = true;

        Debug.Log("[Process] Injection complete!");
    }

    Texture2D LoadTexture(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError("Image not found: " + path);
            return null;
        }

        byte[] data = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);

        if (!tex.LoadImage(data))
        {
            Debug.LogError("Failed to load texture: " + path);
            return null;
        }

        return tex;
    }

    IEnumerator AddImage(Texture2D tex, string name, float width, ARTrackedImageManager manager)
    {
        var library = manager.referenceLibrary as MutableRuntimeReferenceImageLibrary;

        if (library == null)
        {
            Debug.LogError("Image library is not mutable!");
            yield break;
        }
        

        var job = library.ScheduleAddImageWithValidationJob(tex, name, width);
        yield return job;
        Debug.Log("Added image to AR library: " + name);
    }
    async void LoadGLBModel(string path, string imageName)
    {
        var gltf = new GLTFast.GltfImport();
        bool success = await gltf.Load("file://" + path);

        if (!success)
        {
            Debug.LogError("Failed to load GLB: " + path);
            return;
        }

        // 1. Create a container just for the raw meshes
        GameObject meshContainer = new GameObject(imageName + "_mesh");
        await gltf.InstantiateMainSceneAsync(meshContainer.transform);

        // 2. Normalize the meshes to exactly 1 meter and center them
        NormalizeSizeAndCenter(meshContainer, 1f);

        // 🔥 3. Create the clean Wrapper Box for the tracker to scale
        GameObject trackingWrapper = new GameObject(imageName);
        meshContainer.transform.SetParent(trackingWrapper.transform, false);

        trackingWrapper.SetActive(false);

        // Hand the wrapper to the tracker, not the raw mesh
        modelMap[imageName] = trackingWrapper;

        Debug.Log($"[GLB] Loaded model for {imageName}");
    }
    private void NormalizeSizeAndCenter(GameObject container, float targetSizeInMeters)
    {
        Renderer[] renderers = container.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        // 1. Calculate initial bounds to find the scale
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            bounds.Encapsulate(r.bounds);
        }

        // 2. Apply scale to the parent container
        float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxDimension > 0.0001f)
        {
            float scaleFactor = targetSizeInMeters / maxDimension;
            container.transform.localScale = Vector3.one * scaleFactor;
        }

        // 3. Recalculate bounds AFTER scaling
        // (Because the container shrank, the world-space bounds have changed)
        bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            bounds.Encapsulate(r.bounds);
        }

        // 4. Calculate the offset
        // We find the exact bottom-center of the newly scaled 3D model
        Vector3 bottomCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        
        // We calculate how far off that point is from our container's true origin
        Vector3 offset = container.transform.position - bottomCenter;

        // 5. Shift the meshes inside the container
        // This physically moves the 3D model so its bottom-center rests perfectly on (0,0,0)
        foreach (Transform child in container.transform)
        {
            child.position += offset;
        }
    }


}