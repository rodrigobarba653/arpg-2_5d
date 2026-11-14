using UnityEngine;

public class ScrollSeabedTexture : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Vector2 direction = new Vector2(-1f, 0.25f);
    [SerializeField] private float speed = 0.01f;

    private Material materialInstance;
    private Vector2 offset;

    void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        materialInstance = targetRenderer.material;
    }

    void Update()
    {
        offset += direction.normalized * speed * Time.deltaTime;
        materialInstance.SetTextureOffset("_BaseMap", offset);
    }
}