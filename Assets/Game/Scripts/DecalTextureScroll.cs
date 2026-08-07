using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class DecalTextureScroll : MonoBehaviour
{
    public float speedX = 0.02f;
    public float speedY = 0.0f;

    private DecalProjector decal;
    private Vector2 startBias;

    void OnEnable()
    {
        decal = GetComponent<DecalProjector>();

        if (decal != null)
            startBias = decal.uvBias;
    }

    void Update()
    {
        if (decal == null) return;

        Vector2 offset = startBias;
        offset.x += speedX * Time.time;
        offset.y += speedY * Time.time;

        offset.x = Mathf.Repeat(offset.x, 1f);
        offset.y = Mathf.Repeat(offset.y, 1f);

        decal.uvBias = offset;
    }
}