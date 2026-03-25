using UnityEngine;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    public int currentHealth;

    Renderer rend;
    Material mat;

    Color originalColor;

    static readonly int TintColorID = Shader.PropertyToID("_TintColor");

    void Awake()
    {
        currentHealth = maxHealth;

        rend = GetComponentInChildren<Renderer>();

        if (rend != null)
        {
            mat = rend.material;

            if (mat.HasProperty(TintColorID))
                originalColor = mat.GetColor(TintColorID);
        }
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);

        Debug.Log($"[PlayerHealth] {name} took {amount} damage. Current Health = {currentHealth}", this);

        if (mat != null)
            StartCoroutine(FlashRed());

        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        Debug.Log("Player Dead");
    }

    IEnumerator FlashRed()
    {
        mat.SetColor(TintColorID, Color.red);

        yield return new WaitForSeconds(0.08f);

        mat.SetColor(TintColorID, originalColor);
    }
}