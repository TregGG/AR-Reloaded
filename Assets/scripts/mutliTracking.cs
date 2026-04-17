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
        Debug.Log($"[MultiTracking] Updating image: {trackedImage.referenceImage.name}, TrackingState: {trackedImage.trackingState}");
        if (trackedImage == null) return;

        string name = trackedImage.referenceImage.name.ToLower();

        if (!m_ArPrefabs.ContainsKey(name))
        {
            Debug.LogWarning("No model found for: " + name);
            return;
        }

        var obj = m_ArPrefabs[name];

        if (trackedImage.trackingState == TrackingState.None ||
            trackedImage.trackingState == TrackingState.Limited)
        {
            obj.SetActive(false);
            return;
        }
        Debug.Log($"[MultiTracking] Activating model for: {name}");
        obj.SetActive(true);

        if (obj.transform.parent != trackedImage.transform)
        {
            obj.transform.SetParent(trackedImage.transform);

            obj.transform.localPosition = new Vector3(0,0,0);
            obj.transform.localRotation = Quaternion.identity;
            // obj.transform.localScale = Vector3.one; // Ensure scale is applied correctly
        }
        Debug.Log($"[MultiTracking] Model position before update: {obj.transform.position}, rotation: {obj.transform.rotation}");

    }
}