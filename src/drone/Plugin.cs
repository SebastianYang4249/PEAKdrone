using BepInEx;
using BepInEx.Logging;
using UnityEngine;

[BepInPlugin("com.SebastianYang.DronePlugin", "Drone Plugin", "1.0.0")]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    private GameObject? droneObject;
    private bool isDroneActive = false;

    private void Awake() {
        Log = Logger;

        Log.LogInfo($"Plugin [Drone Plugin] is loaded!");
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.T))
        {
            Log.LogInfo("[Drone Plugin]    T key pressed, toggling drone mode.");
            ToggleDrone();
        }
    }

    void ToggleDrone() {
        isDroneActive = !isDroneActive;
        Log.LogInfo($"[Drone Plugin]    Drone mode is now {(isDroneActive ? "active" : "inactive")}.");

        if (isDroneActive) {
            if (Character.localCharacter == null) {
                Log.LogError("[Drone Plugin]    No local character found, cannot activate drone mode.");
                return;
            }

            if (MainCamera.instance == null) {
                Log.LogError("[Drone Plugin]    No main camera found, cannot activate drone mode.");
                return;
            }

            Log.LogInfo("[Drone Plugin]    Pass all cheaks, activating drone mode...");

            Log.LogInfo("[Drone Plugin]    Creating drone camera object...");
            droneObject = new GameObject("DroneCamera");
            droneObject.transform.position = Character.localCharacter.transform.position + Vector3.up * 2f;

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
