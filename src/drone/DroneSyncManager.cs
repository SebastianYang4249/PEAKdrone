using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;

// 这个类将处理所有关于无人机标点的网络同步
// 它应该被附加到一个带有 PhotonView 的 GameObject 上
public class DroneSyncManager : MonoBehaviourPun
{
    // 单例模式，方便从任何地方访问
    public static DroneSyncManager Instance { get; private set; }

    // 用于存储在本地创建的标点和线条的游戏对象
    // 每个客户端都需要维护自己的列表，以响应RPC进行增删
    private List<GameObject> markerSpheres = new List<GameObject>();
    private List<GameObject> markerLines = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            // 防止重复实例
            Destroy(gameObject);
        }
    }

    #region RPC Calls - 由本地玩家调用，向网络发送请求

    public void RequestPlaceMarker(Vector3 position)
    {
        // 调用RPC，让所有客户端（包括自己）在指定位置放置一个标点
        photonView.RPC("RPC_PlaceMarker", RpcTarget.All, position);
    }

    public void RequestCancelLastMarker()
    {
        // 调用RPC，让所有客户端（包括自己）删除最后一个标点
        photonView.RPC("RPC_CancelLastMarker", RpcTarget.All);
    }

    public void RequestClearAllMarkers()
    {
        // 调用RPC，让所有客户端（包括自己）清除所有标点
        photonView.RPC("RPC_ClearAllMarkers", RpcTarget.All);
    }

    #endregion

    #region RPC Implementations - 由PUN网络调用，在所有客户端上执行

    [PunRPC]
    private void RPC_PlaceMarker(Vector3 position)
    {
        // --- 这部分逻辑基本是从你的 Plugin.cs 中移动过来的 ---
        GameObject newSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        newSphere.transform.position = position + Vector3.up * 0.2f; // 使用RPC传过来的位置
        newSphere.transform.localScale = Vector3.one * 0.7f;
        
        var sphereCollider = newSphere.GetComponent<Collider>();
        if (sphereCollider != null) Destroy(sphereCollider);

        var sphereRenderer = newSphere.GetComponent<Renderer>();
        if (sphereRenderer != null) sphereRenderer.material.color = Color.yellow;
            
        markerSpheres.Add(newSphere);
        Plugin.Log.LogInfo($"[Drone Sync] Received RPC to place marker #{markerSpheres.Count} at {position}.");

        // 如果已经有标点，则创建连接线
        if (markerSpheres.Count > 1)
        {
            GameObject previousSphere = markerSpheres[markerSpheres.Count - 2];
            GameObject newLine = CreateGroundClampedLine(previousSphere.transform.position, newSphere.transform.position);
            markerLines.Add(newLine);
        }
    }

    [PunRPC]
    private void RPC_CancelLastMarker()
    {
        if (markerSpheres.Count > 0)
        {
            GameObject lastSphere = markerSpheres[markerSpheres.Count - 1];
            markerSpheres.RemoveAt(markerSpheres.Count - 1);
            Destroy(lastSphere);
            Plugin.Log.LogInfo("[Drone Sync] Received RPC to remove the last marker.");
        }

        if (markerLines.Count > 0)
        {
            GameObject lastLine = markerLines[markerLines.Count - 1];
            markerLines.RemoveAt(markerLines.Count - 1);
            Destroy(lastLine);
            Plugin.Log.LogInfo("[Drone Sync] Received RPC to remove the last line.");
        }
    }

    [PunRPC]
    private void RPC_ClearAllMarkers()
    {
        Plugin.Log.LogInfo("[Drone Sync] Received RPC to clear all markers and lines...");
        foreach (var sphere in markerSpheres)
        {
            Destroy(sphere);
        }
        foreach (var line in markerLines)
        {
            Destroy(line);
        }
        markerSpheres.Clear();
        markerLines.Clear();
    }

    #endregion

    // 这个辅助方法也从 Plugin.cs 移到这里，因为它被RPC方法使用
    private GameObject CreateGroundClampedLine(Vector3 start, Vector3 end)
    {
        var lineObject = new GameObject("MarkerLine_Synced");
        var lineRenderer = lineObject.AddComponent<LineRenderer>();

        int segments = 20;
        var points = new List<Vector3>();

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 pointOnStraightLine = Vector3.Lerp(start, end, t);

            RaycastHit groundHit;
            // 向上偏移量增加到50，向下射线长度也增加到100，以确保能击中更远处的地面
            if (Physics.Raycast(pointOnStraightLine + Vector3.up * 5f, Vector3.down, out groundHit, 5f))
            {
                points.Add(groundHit.point + Vector3.up * 0.1f);
            }
            else
            {
                points.Add(pointOnStraightLine);
            }
        }

        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPositions(points.ToArray());

        lineObject.AddComponent<AnimatedLine>();

        return lineObject;
    }
}