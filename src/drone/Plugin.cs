using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.InputSystem;

[BepInPlugin("com.SebastianYang.DronePlugin", "Drone Plugin", "1.0.0")]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    private GameObject? droneObject;
    private bool isDroneActive = false;

    public float moveSpeed = 15f; 
    public float rotationSpeed = 100f;

    private void Awake() {
        Log = Logger;
        Log.LogInfo($"[Drone Plugin]    Plugin is loaded!");

        // 【关键】订阅 InputManager 的 onBeforeUpdate 事件
        InputSystem.onBeforeUpdate += OnBeforeInputUpdate;
    }

    void OnDestroy()
    {
        // 在插件卸载时，取消订阅，避免内存泄漏
        InputSystem.onBeforeUpdate -= OnBeforeInputUpdate;
    }

    private void OnBeforeInputUpdate()
    {
        // 如果无人机是激活状态，就在输入系统处理任何事情之前，将玩家输入清零
        if (isDroneActive && Character.localCharacter != null)
        {
            // 直接访问并清零，此时做的修改将在本帧的输入处理中生效
            Character.localCharacter.input.movementInput = Vector2.zero;
            Character.localCharacter.input.lookInput = Vector2.zero;
            Character.localCharacter.input.jumpWasPressed = false;
            Character.localCharacter.input.sprintIsPressed = false;
            // ... 你可以根据需要添加更多要清零的变量 ...
        }
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.T))
        {
            Log.LogInfo("[Drone Plugin]    T key pressed, toggling drone mode.");
            ToggleDrone();
        }

        if (isDroneActive)
        {
            MoveDrone();
        }
    }

    
    void ToggleDrone()
    {
        isDroneActive = !isDroneActive;
        Log.LogInfo($"无人机模式切换为: {isDroneActive}");

        if (isDroneActive)
        {
            if (Character.localCharacter == null || MainCamera.instance == null) {
                Log.LogError("激活失败：玩家或摄像机不存在！");
                isDroneActive = false;
                return;
            }

            // --- 激活无人机 ---
            droneObject = new GameObject("MyDrone_Final");
            droneObject.transform.position = Character.localCharacter.Head + (Character.localCharacter.transform.up * 0.5f) + (Character.localCharacter.transform.forward * 1f);
            
            var cameraOverride = droneObject.AddComponent<CameraOverride>();
            cameraOverride.fov = 70f;
            cameraOverride.DoOverride();
        }
        else
        {
            // --- 关闭无人机 ---
            if (MainCamera.instance != null) {
                MainCamera.instance.SetCameraOverride(null!);
            }
            if (droneObject != null) {
                Destroy(droneObject);
            }
        }
    }

    // 【无人机移动逻辑】
    void MoveDrone()
    {
        if (droneObject == null) return;

        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        Vector3 moveDirection = Vector3.zero;
        if (keyboard.wKey.isPressed) moveDirection += droneObject.transform.forward;
        if (keyboard.sKey.isPressed) moveDirection -= droneObject.transform.forward;
        if (keyboard.dKey.isPressed) moveDirection += droneObject.transform.right;
        if (keyboard.aKey.isPressed) moveDirection -= droneObject.transform.right;
        if (keyboard.spaceKey.isPressed) moveDirection += Vector3.up;
        if (keyboard.leftCtrlKey.isPressed) moveDirection -= Vector3.up;
        
        droneObject.transform.position += moveDirection.normalized * moveSpeed * Time.deltaTime;

        Vector2 lookInput = mouse.delta.ReadValue() * 0.1f;
        droneObject.transform.Rotate(Vector3.up, lookInput.x * rotationSpeed * Time.deltaTime, Space.World);
        droneObject.transform.Rotate(Vector3.right, -lookInput.y * rotationSpeed * Time.deltaTime, Space.Self);

        if (mouse.leftButton.wasPressedThisFrame)
        {
            PlaceMarker();
        }
    }
    
    // 【标记点逻辑】
    void PlaceMarker()
    {
        if (droneObject == null) return;
        RaycastHit hit;
        if (Physics.Raycast(droneObject.transform.position, droneObject.transform.forward, out hit, 5000f))
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.position = hit.point;
            marker.transform.localScale = Vector3.one * 0.5f;
            var collider = marker.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var renderer = marker.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = Color.red;
        }
    }
}
