using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Swaps the spritesheet chrome on a chatbox from speaker ↔ bubble geometry.
/// </summary>
[DisallowMultipleComponent]
public class DialogueChatboxSheetView : MonoBehaviour
{
    public ChatboxSheetCatalog catalog;
    public Image chromeImage;
    public ChatboxSheetColumn column = ChatboxSheetColumn.LargePlain;
    public ChatboxSheetTail currentTail = ChatboxSheetTail.NoTail;
    public DialogueChatboxSize chatboxSize = DialogueChatboxSize.Large;
    public bool allowTail = true;

    public void Configure(ChatboxSheetColumn col, bool withTail)
    {
        column = col;
        allowTail = withTail;
        ApplyTail(allowTail ? currentTail : ChatboxSheetTail.NoTail);
    }

    public void SetHasPortrait(bool hasPortrait)
    {
        if (catalog == null)
            return;
        column = catalog.ResolveColumn(chatboxSize, hasPortrait);
        ApplyTail(currentTail);
    }

    /// <summary>Pick and apply tail from bubble/speaker screen positions.</summary>
    public void RefreshTailFromSpeaker(Vector2 speakerScreen)
    {
        if (catalog == null || chromeImage == null)
            return;

        var view = GetComponent<DialogueChatboxView>();
        Vector2 center = speakerScreen;
        if (view != null)
        {
            Rect r = view.GetScreenRect();
            center = r.center;
        }

        var next = ChatboxSheetTailPicker.Pick(center, speakerScreen, allowTail);
        ApplyTail(next);
    }

    public void ApplyTail(ChatboxSheetTail tail)
    {
        if (!allowTail)
            tail = ChatboxSheetTail.NoTail;

        currentTail = tail;
        if (catalog == null || chromeImage == null)
            return;

        var sp = catalog.Get(column, tail);
        if (sp != null)
            chromeImage.sprite = sp;
    }
}
