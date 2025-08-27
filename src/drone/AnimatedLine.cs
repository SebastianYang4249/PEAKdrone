using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class AnimatedLine : MonoBehaviour
{
    // 公开属性，方便在其他地方调整
    public float animationSpeed = -1.5f; // 负数表示从起点流向终点
    public Color lineColor = Color.cyan;
    [Range(0.05f, 0.5f)]
    public float lineWidth = 0.2f;

    private LineRenderer lineRenderer;
    private Material lineMaterial;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        SetupAnimatedMaterial();
    }

    private void SetupAnimatedMaterial()
    {
        // 创建一个新的材质，使用一个支持纹理和透明度的粒子着色器
        lineMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));

        // 以编程方式创建一个简单的虚线纹理
        Texture2D dashTexture = CreateDashTexture();
        
        // 将纹理应用到材质上
        lineMaterial.mainTexture = dashTexture;
        
        // 设置纹理的平铺和颜色
        lineMaterial.SetColor("_TintColor", lineColor);
        
        // 将新创建的材质赋给 LineRenderer
        lineRenderer.material = lineMaterial;
        
        // 设置线宽
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
    }

    // 这个方法动态地创建一个 32x1 像素的纹理，中间是白色，两边是透明，形成一个“短划线”
    private Texture2D CreateDashTexture()
    {
        int width = 32;
        Texture2D texture = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Repeat; // 必须设置为Repeat才能滚动

        Color transparent = new Color(0, 0, 0, 0);
        Color cyan = Color.cyan;

        // 填充为全透明
        for (int i = 0; i < width; i++)
        {
            texture.SetPixel(i, 0, transparent);
        }

        // 在中间绘制一条白色的线段 (例如，从像素8到像素24)
        for (int i = width / 4; i < width * 3 / 4; i++)
        {
            texture.SetPixel(i, 0, cyan);
        }

        texture.Apply();
        return texture;
    }

    void Update()
    {
        if (lineMaterial != null)
        {
            // 这是动画的核心：随着时间的推移，不断改变材质上纹理的X轴偏移量
            // Time.time 保证了动画的平滑和帧率无关
            // 使用 % 1f (取模) 是为了让偏移值始终保持在 0-1 之间，防止数值无限增大
            float offset = (Time.time * animationSpeed) % 1f;
            
            // 应用偏移量
            lineMaterial.mainTextureOffset = new Vector2(offset, 0);

            // 根据线条的实际长度调整纹理的平铺，让虚线看起来大小一致
            if (lineRenderer.positionCount > 1)
            {
                float lineDistance = Vector3.Distance(lineRenderer.GetPosition(0), lineRenderer.GetPosition(lineRenderer.positionCount - 1));
                lineMaterial.mainTextureScale = new Vector2(lineDistance / (lineWidth * 10), 1);
            }
        }
    }
}