using System.Collections;
using UnityEngine;

/// <summary>Runtime ambient bubble tied to one line (Phase A: single-line bursts from host).</summary>
public class AmbientDialogueHandle
{
    public bool IsDead { get; private set; }

    readonly DialogueService service;
    readonly NpcDialogueHost host;
    readonly DialogueAsset asset;
    readonly DialogueLineNode line;
    readonly Transform anchor;
    readonly Vector2 manualOffset;
    readonly DialogueChatboxPlacement preferred;
    readonly float scale;
    readonly float holdSeconds;

    DialogueChatboxView view;
    MonoBehaviour runner;
    Coroutine routine;
    string fullText;
    int visibleChars;
    bool lineComplete;

    public AmbientDialogueHandle(
        DialogueService service,
        NpcDialogueHost host,
        DialogueAsset asset,
        DialogueLineNode line,
        Transform anchor,
        Vector2 manualOffset,
        DialogueChatboxPlacement preferred,
        float scale,
        float holdSeconds)
    {
        this.service = service;
        this.host = host;
        this.asset = asset;
        this.line = line;
        this.anchor = anchor;
        this.manualOffset = manualOffset;
        this.preferred = preferred;
        this.scale = scale;
        this.holdSeconds = holdSeconds;
    }

    public bool TryShow(DialogueLayoutSolver layout)
    {
        view = service.UI.SpawnAmbient();
        if (scale > 0.01f)
            view.rectTransform.localScale = Vector3.one * scale;

        var layoutSettings = service.LayoutSettings;
        float aw = layoutSettings != null ? layoutSettings.ambientWidthRef : 380f;
        float ah = layoutSettings != null ? layoutSettings.ambientHeightRef : 140f;
        view.rectTransform.sizeDelta = new Vector2(aw, ah);

        string speaker = line.speakerDisplayName;
        if (string.IsNullOrEmpty(speaker) && service.roster != null)
            speaker = service.roster.FindDisplayName(line.speakerId);
        if (string.IsNullOrEmpty(speaker))
            speaker = line.speakerId;

        view.SetSpeaker(speaker);
        view.SetPortrait(null); // Ambient never shows portraits.
        fullText = line.text ?? "";
        visibleChars = 0;
        lineComplete = string.IsNullOrEmpty(fullText);
        view.PrepareBodyFit(fullText);
        view.SetBodyVisibleText("");
        view.SetVisible(true);
        DialogueVoice.PlayLine(line);

        Vector2 screen = WorldToScreen(anchor != null ? anchor.position : host.transform.position);
        bool placed = layout.TryPlaceAmbient(
            view, screen, preferred, manualOffset, view.rectTransform.rect.size);

        if (!placed)
            return false;

        runner = service;
        routine = service.StartCoroutine(Play());
        return true;
    }

    public void TickLayout(DialogueLayoutSolver layout, bool interactiveActive)
    {
        if (IsDead || view == null)
            return;

        if (interactiveActive)
        {
            // Must never overlap interactive — dismiss (host cooldown still applies).
            Dismiss(suppressCooldown: false);
            return;
        }

        Vector2 screen = WorldToScreen(anchor != null ? anchor.position : host.transform.position);
        view.FitHeightToBody();
        if (!layout.TryPlaceAmbient(view, screen, preferred, manualOffset, view.rectTransform.rect.size))
        {
            // No space this frame — suppress.
            Dismiss(suppressCooldown: false);
        }
    }

    public void Dismiss(bool suppressCooldown)
    {
        if (IsDead)
            return;

        IsDead = true;
        DialogueVoice.Stop();
        if (runner != null && routine != null)
            runner.StopCoroutine(routine);

        if (view != null)
        {
            service.UI.Release(view);
            view = null;
        }

        service.UnregisterAmbient(this);
        host?.NotifyAmbientFinished(suppressCooldown);
    }

    IEnumerator Play()
    {
        float cps = line.typewriterCharsPerSecond > 0f
            ? line.typewriterCharsPerSecond
            : (asset != null ? asset.defaultTypewriterCharsPerSecond : 40f);
        if (cps < 1f) cps = 1f;

        float acc = 0f;
        while (!lineComplete && !IsDead)
        {
            acc += Time.deltaTime * cps;
            int add = Mathf.FloorToInt(acc);
            if (add > 0)
            {
                acc -= add;
                visibleChars = Mathf.Min(fullText.Length, visibleChars + add);
                view.SetBodyVisibleText(fullText.Substring(0, visibleChars));
                if (visibleChars >= fullText.Length)
                    lineComplete = true;
            }
            yield return null;
        }

        float hold = holdSeconds;
        if (line.ambientHoldSeconds > 0f)
            hold = line.ambientHoldSeconds;
        if (hold < 0.05f)
            hold = 1.5f;

        float t = 0f;
        while (t < hold && !IsDead)
        {
            t += Time.deltaTime;
            yield return null;
        }

        Dismiss(suppressCooldown: false);
    }

    static Vector2 WorldToScreen(Vector3 world) => DialogueScreenUtility.WorldToScreen(world);
}
