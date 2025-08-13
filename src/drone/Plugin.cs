using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[BepInPlugin("com.SebastianYang.DronePlugin", "Drone Plugin", "1.0.0")]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    private GameObject? droneObject;
    private bool isDroneActive = false;

    private CharacterMovement playerMovementComponent;

    public float moveSpeed = 15f; 
    public float rotationSpeed = 100f;

    private void Awake() {
        Log = Logger;
        Log.LogInfo($"[Drone Plugin]    Plugin is loaded!");
    }

    void Update()
    {
        // Update 中只负责两件事：监听按键 和 移动无人机
        if (Input.GetKeyDown(KeyCode.T))
        {
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

        if (Character.localCharacter != null && playerMovementComponent == null)
        {
            playerMovementComponent = Character.localCharacter.refs.movement;
        }
        
        if (playerMovementComponent == null)
        {
            Log.LogError("无法获取 CharacterMovement 组件，操作中止。");
            isDroneActive = false;
            return;
        }

        if (isDroneActive)
        {
            // --- 冻结玩家 ---
            playerMovementComponent.enabled = false;
            Log.LogInfo("CharacterMovement 组件已禁用。");

            // 【启用物理重力】
            // 遍历角色的所有肢体，为它们开启 Unity 的原生重力
            foreach (var bodypart in Character.localCharacter.refs.ragdoll.partList)
            {
                if (bodypart.Rig != null)
                {
                    bodypart.Rig.useGravity = true;
                }
            }
            Log.LogInfo("所有肢体的原生重力已启用，玩家将被固定在地面上。");

            // --- 激活无人机 ---
            if (MainCamera.instance == null) {
                // 如果出错，记得恢复所有状态
                isDroneActive = false;
                EnablePlayerControl(); 
                return;
            }
            droneObject = new GameObject("MyDrone_Newtonian");
            droneObject.transform.position = Character.localCharacter.Head + (Character.localCharacter.transform.up * 0.5f) + (Character.localCharacter.transform.forward * 1f);
            
            var cameraOverride = droneObject.AddComponent<CameraOverride>();
            cameraOverride.fov = 70f;
            cameraOverride.DoOverride();
        }
        else
        {
            // --- 恢复玩家控制 ---
            EnablePlayerControl();

            // --- 关闭无人机 ---
            if (MainCamera.instance != null) {
                MainCamera.instance.SetCameraOverride(null!);
            }
            if (droneObject != null) {
                Destroy(droneObject);
            }
        }
    }

    void EnablePlayerControl()
    {
        if (playerMovementComponent == null || Character.localCharacter == null) return;

        // 【恢复游戏控制】
        // 必须先关闭原生重力，再启用运动组件
        foreach (var bodypart in Character.localCharacter.refs.ragdoll.partList)
        {
            if (bodypart.Rig != null)
            {
                bodypart.Rig.useGravity = false;
            }
        }
        Log.LogInfo("所有肢体的原生重力已关闭。");
        
        playerMovementComponent.enabled = true;
        Log.LogInfo("CharacterMovement 组件已恢复。");
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
