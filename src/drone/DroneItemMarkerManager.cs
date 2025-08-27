using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class DroneItemMarkerManager : MonoBehaviour
{
    public static DroneItemMarkerManager Instance { get; private set; }

    // --- 新增：可配置的显示范围 ---
    [Header("性能与显示设置")]
    [Tooltip("只显示此距离（米）以内的物品标点")]
    public float maxDisplayDistance = 100f; 
    
    [Tooltip("每隔多少秒重新扫描一次场景中的物品")]
    public float scanInterval = 2.0f;
    
    [Tooltip("距离变化超过多少米才更新一次UI文本，减少GC")]
    private const float DistanceUpdateThreshold = 1.0f; // 距离变化超过1米才更新文本

    private Canvas markerCanvas;
    private Camera droneCamera;
    private Transform droneTransform;

    // --- 优化：使用一个专门的类来存储每个标点的信息 ---
    private class MarkerInfo
    {
        public TextMeshProUGUI Label;
        public Transform TargetTransform;
        public Item TargetItem; // 缓存Item组件
        public string CachedItemName; // 缓存物品名称
        public float LastKnownDistance;
    }
    private readonly Dictionary<Transform, MarkerInfo> _activeMarkers = new Dictionary<Transform, MarkerInfo>();

    private bool isRunning = false;
    private float nextScanTime = 0f;
    private float maxDisplayDistanceSqr; // 缓存平方距离，用于优化

    private static readonly System.Type[] ScanTypes = { typeof(Item) };

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateCanvas();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void StartManager(Camera cam, Transform drone)
    {
        droneCamera = cam;
        droneTransform = drone;
        isRunning = true;
        markerCanvas.gameObject.SetActive(true);
        nextScanTime = Time.time;
        maxDisplayDistanceSqr = maxDisplayDistance * maxDisplayDistance; // 预先计算平方距离
    }

    public void StopManager()
    {
        isRunning = false;
        if (markerCanvas != null)
        {
            markerCanvas.gameObject.SetActive(false);
        }
        ClearAllMarkers();
    }

    private void Update()
    {
        if (!isRunning || droneCamera == null || droneTransform == null)
        {
            return;
        }

        if (Time.time >= nextScanTime)
        {
            ScanForTargets();
            nextScanTime = Time.time + scanInterval;
        }

        UpdateMarkerPositions();
    }

    private void ScanForTargets()
    {
        HashSet<Transform> foundTargets = new HashSet<Transform>();
        foreach (var type in ScanTypes)
        {
            foreach (var component in FindObjectsOfType(type))
            {
                var monoBehaviour = component as MonoBehaviour;
                if (IsTargetValid(monoBehaviour))
                {
                    foundTargets.Add(monoBehaviour.transform);
                }
            }
        }
        
        var removedTargets = _activeMarkers.Keys.Except(foundTargets).ToList();
        foreach (var target in removedTargets)
        {
            RemoveMarkerFor(target);
        }
        
        foreach (var target in foundTargets)
        {
            if (!_activeMarkers.ContainsKey(target))
            {
                CreateMarkerFor(target);
            }
        }
    }

    private bool IsTargetValid(MonoBehaviour target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        
        Item item = target.GetComponent<Item>();
        if (item != null)
        {
            return item.itemState == ItemState.Ground;
        }
        return false;
    }

    private void CreateMarkerFor(Transform target)
    {
        Item itemComponent = target.GetComponent<Item>();
        if (itemComponent == null) return;

        GameObject markerObject = new GameObject($"Marker_{target.name}");
        markerObject.transform.SetParent(markerCanvas.transform, false);

        TextMeshProUGUI label = markerObject.AddComponent<TextMeshProUGUI>();

        // --- 核心修改：直接使用从 Plugin 类捕获的静态字体 ---
        if (Plugin.GameFont != null)
        {
            label.font = Plugin.GameFont;
        }
        else
        {
            // 如果因为某些原因字体捕获失败，我们就在日志里发出警告
            // 这样游戏就不会崩溃，只是中文会再次显示为方块
            Plugin.Log.LogWarning("GameFont is null. UI text may not display correctly.");
        }

        // 其他设置保持不变
        label.fontSize = 14;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.cyan;
        label.fontStyle = FontStyles.Bold;
        label.outlineWidth = 0.1f;
        label.outlineColor = Color.black;
        
        var newMarkerInfo = new MarkerInfo
        {
            Label = label,
            TargetTransform = target,
            TargetItem = itemComponent,
            CachedItemName = itemComponent.GetItemName(null),
            LastKnownDistance = -1f
        };

        _activeMarkers[target] = newMarkerInfo;
    }

    private void RemoveMarkerFor(Transform target)
    {
        if (_activeMarkers.TryGetValue(target, out MarkerInfo markerInfo))
        {
            if (markerInfo.Label != null) Destroy(markerInfo.Label.gameObject);
            _activeMarkers.Remove(target);
        }
    }

    private void UpdateMarkerPositions()
    {
        List<Transform> toRemove = new List<Transform>();

        foreach (var pair in _activeMarkers)
        {
            MarkerInfo marker = pair.Value;
            Transform target = marker.TargetTransform;

            if (target == null)
            {
                toRemove.Add(pair.Key);
                Destroy(marker.Label.gameObject);
                continue;
            }

            Vector3 targetPosition = target.position;
            Vector3 dronePosition = droneTransform.position;
            
            // --- 优化 & 新功能：使用平方距离进行范围检查 ---
            float sqrDistance = (targetPosition - dronePosition).sqrMagnitude;
            if (sqrDistance > maxDisplayDistanceSqr)
            {
                marker.Label.enabled = false; // 超出范围，隐藏
                continue; // 直接处理下一个，不做后续计算
            }

            Vector3 screenPos = droneCamera.WorldToScreenPoint(targetPosition);

            if (screenPos.z < 0)
            {
                marker.Label.enabled = false; // 在相机后面，隐藏
            }
            else
            {
                marker.Label.enabled = true;
                marker.Label.rectTransform.position = screenPos;
                
                // --- 优化：仅在距离变化足够大时才更新文本 ---
                float currentDistance = Mathf.Sqrt(sqrDistance); // 只有在需要显示时才计算开方
                if (Mathf.Abs(currentDistance - marker.LastKnownDistance) > DistanceUpdateThreshold)
                {
                    marker.Label.text = $"{marker.CachedItemName}\n[{currentDistance:F1}m]";
                    marker.LastKnownDistance = currentDistance;
                }
            }
        }

        foreach (var key in toRemove)
        {
            _activeMarkers.Remove(key);
        }
    }

    private void ClearAllMarkers()
    {
        foreach (var marker in _activeMarkers.Values)
        {
            if (marker.Label != null) Destroy(marker.Label.gameObject);
        }
        _activeMarkers.Clear();
    }

    private void CreateCanvas()
    {
        GameObject canvasObj = new GameObject("DroneMarkerCanvas");
        markerCanvas = canvasObj.AddComponent<Canvas>();
        markerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        markerCanvas.sortingOrder = 999;
        DontDestroyOnLoad(canvasObj);
        canvasObj.SetActive(false);
    }
}