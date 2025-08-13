using BepInEx;
using BepInEx.Logging;
using UnityEngine;

[BepInPlugin("com.SebastianYang.DronePlugin", "Drone Plugin", "1.0.0")]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    private GameObject? droneObject;
    private bool isDroneActive = false;

    private Vector2 originalMovementInput;
    private Vector2 originalMouseInput;

    private void Awake() {
        Log = Logger;
        Log.LogInfo($"[Drone Plugin]    Plugin is loaded!");
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.T))
        {
            Log.LogInfo("[Drone Plugin]    T key pressed, toggling drone mode.");
            ToggleDrone();
        }

        if (isDroneActive && Character.localCharacter != null && droneObject != null) {
            Character.localCharacter.input.movementInput = Vector2.zero;
            Character.localCharacter.input.lookInput = Vector2.zero;
            Character.localCharacter.input.jumpWasPressed = false;
            Character.localCharacter.input.sprintIsPressed = false;
        }
    }

    void ToggleDrone() {
        isDroneActive = !isDroneActive;
        Log.LogInfo($"[Drone Plugin]    Drone mode is now {(isDroneActive ? "active" : "inactive")}.");

        if (isDroneActive) {
            if (Character.localCharacter == null) {
                Log.LogError("[Drone Plugin]    No local character found, cannot activate drone mode.");
                isDroneActive = false;
                return;
            }

            if (MainCamera.instance == null) {
                Log.LogError("[Drone Plugin]    No main camera found, cannot activate drone mode.");
                isDroneActive = false;
                return;
            }

            Log.LogInfo("[Drone Plugin]    Pass all cheaks, activating drone mode...");

            Log.LogInfo("[Drone Plugin]    Creating drone camera object...");
            droneObject = new GameObject("DroneCamera");
           
            Vector3 headPos = Character.localCharacter.Head;
            Vector3 spawnPos = headPos + Character.localCharacter.transform.up * 2f + Character.localCharacter.transform.forward * 1f;
            droneObject.transform.position = spawnPos;

            Log.LogInfo("[Drone Plugin]    Adding DroneCameraOverride component...");
            var droneController = droneObject.AddComponent<CameraOverride>();

            Log.LogInfo("[Drone Plugin]    Setting camera override...");
            droneController.DoOverride();
        } else {
            Log.LogInfo("[Drone Plugin]    Deactivating drone mode...");

            if (MainCamera.instance == null) {
                Log.LogError("[Drone Plugin]    No main camera found, cannot deactivate drone mode.");
                return;
            }

            MainCamera.instance.SetCameraOverride(null!);

            if (droneObject == null) {
                Log.LogError("[Drone Plugin]    No drone object found, cannot deactivate drone mode.");
                return;
            }

            Destroy(droneObject);
        }
    }
}
