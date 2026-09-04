using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Placeholder sub-tab with no functionality yet — used for "Display" for now.
/// Reusable for any future settings tab that's just a button until its content
/// is built. Shows a static message, nothing interactive.
/// </summary>
public class PlaceholderSettingsPanel : MenuPanel
{
    public TMP_Text messageText;
    public string message = "Coming soon";

    public override void Refresh()
    {
        if (messageText != null) messageText.text = message;
    }

    public override Selectable GetFirstSelectable() => null;
}
