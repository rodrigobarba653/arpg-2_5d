using UnityEngine;

[RequireComponent(typeof(Collider))]
public class InteractivePromptTrigger : MonoBehaviour
{
    [Header("Prompt")]
    public PlayerInteractionPrompt playerPrompt;

    [Header("Player Detection")]
    public string playerTag = "Player";

    int playerInsideCount = 0;

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerInsideCount++;

        if (playerPrompt == null)
            playerPrompt = other.GetComponentInChildren<PlayerInteractionPrompt>(true);

        if (playerPrompt != null)
            playerPrompt.ShowPrompt();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerInsideCount = Mathf.Max(0, playerInsideCount - 1);

        if (playerInsideCount == 0 && playerPrompt != null)
            playerPrompt.HidePrompt();
    }
}