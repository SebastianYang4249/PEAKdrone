using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using HarmonyLib; // 引入 Harmony
using Photon.Pun; // 引入 Photon
using TMPro;

[BepInPlugin("com.SebastianYang.DronePlugin", "Drone Plugin", "1.0.2")]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    private GameObject? droneObject;
    private bool isDroneActive = false;
    private CharacterMovement playerMovementComponent;

    private float currentYaw = 0f;
    private float currentPitch = 0f;

    public float moveSpeed = 10f; 
    public float rotationSpeed = 100f;
    public float maxDistanceFromPlayer = 15f;

    public static TMP_FontAsset GameFont { get; internal set; }
    // private bool isAtMaxDistance = false; // 这部分逻辑暂时注释掉

    // 移除标点和线的列表，它们现在由 DroneSyncManager 管理
    // private List<GameObject> markerSpheres = new List<GameObject>();
    // private List<GameObject> markerLines = new List<GameObject>();

    // 添加一个新的变量来持有我们的管理器实例
    private DroneItemMarkerManager itemMarkerManager;

    private void Awake() {
        Log = Logger;
        // 应用 Harmony 补丁
        // --- 核心修复：在这里创建并初始化我们的管理器 ---
        // 我们将其附加到一个新的、不会被销毁的游戏对象上
        GameObject managerObj = new GameObject("DroneItemMarkerManager_Instance");
        itemMarkerManager = managerObj.AddComponent<DroneItemMarkerManager>();
        DontDestroyOnLoad(managerObj); // 确保管理器在场景切换时依然存在
        Log.LogInfo("[Drone Plugin]    DroneItemMarkerManager initialized.");
        
        new Harmony("com.SebastianYang.DronePlugin.Patch").PatchAll();
        Log.LogInfo($"[Drone Plugin]    Plugin is loaded and patched!");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleDrone();
        }
        
        if (isDroneActive)
        {
            MoveDroneAndHandleMarkers();
        }
    }
    
    // ToggleDrone 和 EnablePlayerControl 方法保持不变
    void ToggleDrone()
    {
        isDroneActive = !isDroneActive;
        Log.LogInfo($"[Drone Plugin]    Drone mode toggled. Now active: {isDroneActive}");

        if (Character.localCharacter != null && playerMovementComponent == null)
        {
            playerMovementComponent = Character.localCharacter.refs.movement;
        }
        
        if (playerMovementComponent == null)
        {
            Log.LogError("[Drone Plugin]    Unable to find CharacterMovement component. Drone mode cannot be toggled.");
            isDroneActive = false;
            return;
        }

        if (isDroneActive)
        {
            playerMovementComponent.enabled = false;
            foreach (var bodypart in Character.localCharacter.refs.ragdoll.partList)
            {
                if (bodypart.Rig != null) bodypart.Rig.isKinematic = true;
            }
            Log.LogInfo("[Drone Plugin]    Player control disabled, original gravity enabled.");

            if (MainCamera.instance == null) {
                isDroneActive = false;
                EnablePlayerControl(); 
                return;
            }
            droneObject = new GameObject("MyDrone_Remote");
            droneObject.transform.position = Character.localCharacter.Head + (Character.localCharacter.transform.up * 0.5f) + (Character.localCharacter.transform.forward * 1f);
            
            var cameraOverride = droneObject.AddComponent<CameraOverride>();
            cameraOverride.fov = 70f;
            cameraOverride.DoOverride();

            Vector3 initialEuler = Character.localCharacter.transform.rotation.eulerAngles;
            currentYaw = initialEuler.y;
            currentPitch = initialEuler.x;

            if (MainCamera.instance != null)
            {
                itemMarkerManager.StartManager(MainCamera.instance.cam, droneObject.transform);
                Log.LogInfo("[Drone Plugin]    Item Marker Manager started.");
            }
        }
        else
        {
            Log.LogInfo("[Drone Plugin]    Item Marker Manager stopped.");
            itemMarkerManager.StopManager();
            
            EnablePlayerControl();
            if (MainCamera.instance != null) MainCamera.instance.SetCameraOverride(null!);
            if (droneObject != null) Destroy(droneObject);
        }
    }

    void EnablePlayerControl()
    {
        if (playerMovementComponent == null || Character.localCharacter == null) return;

        foreach (var bodypart in Character.localCharacter.refs.ragdoll.partList)
        {
            if (bodypart.Rig != null)
            {
                bodypart.Rig.isKinematic = false;
            }
        }
        Log.LogInfo("[]Drone Plugin]    Original gravity disabled.");
        
        playerMovementComponent.enabled = true;
        Log.LogInfo("[Drone Plugin]    Player control re-enabled.");
    }
    
    void MoveDroneAndHandleMarkers()
    {
        if (droneObject == null || Character.localCharacter == null) return;
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        // --- 1. 平滑的旋转逻辑 ---
        
        // 获取鼠标输入并乘以灵敏度
        Vector2 lookInput = mouse.delta.ReadValue() * 0.1f; // 可以将 0.1f 调整为旋转灵敏度
        
        // 累积旋转角度
        currentYaw += lookInput.x * rotationSpeed * Time.deltaTime;
        currentPitch -= lookInput.y * rotationSpeed * Time.deltaTime; // 鼠标向上移动是负值，所以用减法
        
        // 限制俯仰角，防止摄像头翻转
        currentPitch = Mathf.Clamp(currentPitch, -89f, 89f);
        
        // 应用最终的旋转
        droneObject.transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        
        // --- 2. 解耦的移动逻辑 ---
        
        // 获取键盘输入
        Vector3 moveInput = Vector3.zero;
        if (keyboard.wKey.isPressed) moveInput.z = 1;
        if (keyboard.sKey.isPressed) moveInput.z = -1;
        if (keyboard.dKey.isPressed) moveInput.x = 1;
        if (keyboard.aKey.isPressed) moveInput.x = -1;
        
        // 将局部方向的输入转换为世界空间的移动向量
        // 这样 W 永远是无人机机头的“前”
        Vector3 moveDirection = droneObject.transform.forward * moveInput.z + droneObject.transform.right * moveInput.x;
        
        // 处理垂直移动 (上升/下降)
        if (keyboard.spaceKey.isPressed) moveDirection.y = 1;
        if (keyboard.leftCtrlKey.isPressed) moveDirection.y = -1;
        
        // 应用位移，确保移动速度不受朝向影响
        droneObject.transform.position += moveDirection.normalized * moveSpeed * Time.deltaTime;

        // --- 3. 标点放置逻辑 (保持不变) ---
        if (mouse.leftButton.wasPressedThisFrame)
        {
            RaycastHit hit;
            if (Physics.Raycast(droneObject.transform.position, droneObject.transform.forward, out hit, 10000f))
            {
                DroneSyncManager.Instance?.RequestPlaceMarker(hit.point);
            }
        }
        if (mouse.rightButton.wasPressedThisFrame)
        {
            DroneSyncManager.Instance?.RequestCancelLastMarker();
        }
        if (keyboard.rKey.wasPressedThisFrame)
        {
            DroneSyncManager.Instance?.RequestClearAllMarkers();
        }
    }

    // 移除 PlaceMarker, CreateGroundClampedLine, CancelLastMarker, ClearAllMarkers
    // 这些方法的逻辑已经全部移至 DroneSyncManager.cs
}

// --- 添加 Harmony 补丁类来注入我们的管理器 ---
[HarmonyPatch]
public static class Patch
{
    // 就像 TeamRaceManager 一样，我们将 DroneSyncManager 附加到 RunManager 上
    // 这样能确保它在每个游戏回合（"run"）中都存在
    [HarmonyPatch(typeof(RunManager), "Awake")]
    [HarmonyPostfix]
    public static void AddDroneSyncManager(RunManager __instance)
    {
        if (__instance.gameObject.GetComponent<DroneSyncManager>() == null)
        {
            var syncManager = __instance.gameObject.AddComponent<DroneSyncManager>();
            
            // 最重要的一步：为它添加并分配一个 PhotonView
            var photonView = __instance.gameObject.GetComponent<PhotonView>();
            if (photonView == null) {
                photonView = __instance.gameObject.AddComponent<PhotonView>();
            }
            
            Plugin.Log.LogInfo("DroneSyncManager has been added to the RunManager.");
        }
    }
}

[HarmonyPatch(typeof(GUIManager), "InitReticleList")] // 这是一个很好的修补目标
public static class FontCapturePatch
{
    // 使用 Postfix，意味着我们的代码会在原始的 InitReticleList 方法执行完毕后运行
    [HarmonyPostfix]
    public static void CaptureGameFont(GUIManager __instance)
    {
        // 如果我们已经成功捕获过字体，就没必要再执行了
        if (Plugin.GameFont != null)
        {
            return;
        }

        try
        {
            Plugin.Log.LogInfo("Attempting to capture game's main font...");
            
            // 策略：在 GUIManager 的子对象中找到任何一个 TextMeshProUGUI 组件
            // 它的 .font 属性就是我们想要的字体资产
            TMP_Text foundText = __instance.GetComponentInChildren<TMP_Text>(true); // true 表示也搜索被禁用的子对象

            if (foundText != null && foundText.font != null)
            {
                // 成功找到！将其存入我们的静态变量
                Plugin.GameFont = foundText.font;
                Plugin.Log.LogInfo($"Successfully captured font: [{Plugin.GameFont.name}]");
            }
            else
            {
                // 如果在 GUIManager 中没找到，我们可以扩大搜索范围到整个场景
                // 这是一个备用方案，通常都能成功
                Plugin.Log.LogWarning("Could not find font in GUIManager, searching entire scene as a fallback...");
                TMP_Text[] allTexts = Object.FindObjectsOfType<TMP_Text>(true);
                foreach(var text in allTexts)
                {
                    if(text.font != null && text.font.atlasPopulationMode == AtlasPopulationMode.Dynamic) // 动态字体通常是主字体
                    {
                        Plugin.GameFont = text.font;
                        Plugin.Log.LogInfo($"Successfully captured font via fallback method: [{Plugin.GameFont.name}]");
                        return; // 找到后立刻退出循环
                    }
                }
                
                Plugin.Log.LogError("Failed to capture any valid TMP_FontAsset from the scene!");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"An error occurred while capturing font: {ex.Message}");
        }
    }
}