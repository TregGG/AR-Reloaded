using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class MultiTracking : MonoBehaviour
{
    private ARTrackedImageManager m_TrackedImageManager;

    private Dictionary<string, GameObject> m_ArPrefabs;

    void Start()
    {
        m_TrackedImageManager = GetComponent<ARTrackedImageManager>();

        if (m_TrackedImageManager == null)
        {
            Debug.LogError("ARTrackedImageManager not found!");
            return;
        }

        m_ArPrefabs = ContentManager.Instance.modelMap;

        m_TrackedImageManager.trackablesChanged.AddListener(OnImageTrackedChanged);
        Debug.Log("MultiTracking initialized and listener added."); 
        Debug.Log("[MultiTracking] ModelMap keys: " + string.Join(", ", ContentManager.Instance.modelMap.Keys));
    }

    void OnDestroy()
    {
        if (m_TrackedImageManager != null)
            m_TrackedImageManager.trackablesChanged.RemoveListener(OnImageTrackedChanged);
    }

    private void OnImageTrackedChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        // Debug.Log($"[MultiTracking] Trackables changed. Added: {eventArgs.added.Count}, Updated: {eventArgs.updated.Count}, Removed: {eventArgs.removed.Count}");
        foreach (var trackedImage in eventArgs.added)
            UpdateImage(trackedImage);

        foreach (var trackedImage in eventArgs.updated)
            UpdateImage(trackedImage);

        foreach (var trackedImage in eventArgs.removed)
        {
            string name = trackedImage.Value.referenceImage.name.ToLower();

            if (m_ArPrefabs.ContainsKey(name))
                m_ArPrefabs[name].SetActive(false);
        }
    }

    private void UpdateImage(ARTrackedImage trackedImage)
    {
        if (trackedImage == null) return;

        string name = trackedImage.referenceImage.name.ToLower();

        if (!m_ArPrefabs.ContainsKey(name))
        {
            return;
        }

        var obj = m_ArPrefabs[name];

        if (trackedImage.trackingState == TrackingState.None ||
            trackedImage.trackingState == TrackingState.Limited)
        {
            obj.SetActive(false);
            return;
        }

        obj.SetActive(true);

        // 1. Position and Rotation
        // 1. Position and Rotation
        if (obj.transform.parent != trackedImage.transform)
        {
            obj.transform.SetParent(trackedImage.transform, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
        }

        // 🔥 2. DYNAMIC SCALING (Now applied safely to the wrapper!)
        float paperWidth = trackedImage.size.x;
        
        // Safety check: Only scale if ARCore has figured out the real-world size
        if (paperWidth > 0.01f) 
        {
            // You can multiply this by 1.2f if you want the model to pop out larger than the card
            obj.transform.localScale = Vector3.one * paperWidth; 
        } 
    }
}