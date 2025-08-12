using UnityEngine;

public class DroneCameraOverride : MonoBehaviour
{
    public float fov = 60f;

    public float moveSpeed = 10f;
    public float verticalSpeed = 5f;
    public float rotationSpeed = 90f;

    void Awake() {
        // Initialize fov of drone
        this.fov = 60f;
    }

    void Update() {
        // Handle movement
        float horizontalInput = 0f;
        if (Input.GetKey(KeyCode.A))
            horizontalInput = -1f;
        if (Input.GetKey(KeyCode.D))
            horizontalInput = 1f;

        float forwardInput = 0f;
        if (Input.GetKey(KeyCode.W))
            forwardInput = 1f;
        if (Input.GetKey(KeyCode.S))
            forwardInput = -1f;

        float verticalInput = 0f;
        if (Input.GetKey(KeyCode.Space))
            verticalInput = 1f;
        if (Input.GetKey(KeyCode.LeftControl))
            verticalInput = -1f;
        
        // Handle rotation
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        transform.Translate(Vector3.forward * forwardInput * moveSpeed * Time.deltaTime);
        transform.Translate(Vector3.right * horizontalInput * moveSpeed * Time.deltaTime);
        transform.Translate(Vector3.up * verticalInput * verticalSpeed * Time.deltaTime);

        transform.Rotate(Vector3.up, mouseX * rotationSpeed * Time.deltaTime, Space.World);
        transform.Rotate(Vector3.left, mouseY * rotationSpeed * Time.deltaTime, Space.Self);

        // Handle marker
        if (Input.GetKeyDown(KeyCode.F)) {
            PlaceMarker();
        }
    }

    void PlaceMarker() {
        RaycastHit hit;

        if (Physics.Raycast(transform.position, transform.forward, out hit, 1000f)) {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            
            marker.transform.position = hit.point;
            marker.transform.localScale = Vector3.one * 0.5f;
            UnityEngine.Object.Destroy(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().material.color = Color.red;

            Debug.Log("Marker placed at: " + hit.point);
        } else {
            Debug.Log("No surface to place marker.");
        }
    }
}