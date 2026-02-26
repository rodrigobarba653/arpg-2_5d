using UnityEngine;

public class FakeShadow : MonoBehaviour
{
    void LateUpdate()
    {
        // Siempre plano sobre el suelo
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}
