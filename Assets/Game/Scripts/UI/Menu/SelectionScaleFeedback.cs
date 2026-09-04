using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Scales up (and optionally recolors) a target Graphic while THIS GameObject
/// is the current EventSystem selection, reverting when focus leaves. Attach
/// to the SAME GameObject as the Selectable (Slider/Toggle/Button) — Unity's
/// EventSystem calls OnSelect/OnDeselect on every component of the currently
/// selected object, so no extra wiring is needed beyond dropping this on.
///
/// Typical use: put this on a Slider and assign its Handle Image as the
/// target, so the handle visibly grows AND changes color when the slider is
/// focused via gamepad/keyboard navigation (or clicked) — makes it obvious
/// which control is currently selected.
/// </summary>
public class SelectionScaleFeedback : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [Header("Target")]
    [Tooltip("What to scale — usually the slider's Handle. Defaults to this " +
             "GameObject's own RectTransform if left empty. If it also has an " +
             "Image/Graphic, that's used for the optional color change too.")]
    public RectTransform target;

    [Header("Scale")]
    [Tooltip("Scale multiplier applied while selected. 1 = no change, " +
             "1.3 = 30% bigger.")]
    [Range(1f, 2f)]
    public float selectedScale = 1.3f;

    [Header("Color (optional)")]
    [Tooltip("If assigned, this Graphic's color is tweened to 'selectedColor' " +
             "while selected, and back to its original color otherwise. " +
             "Defaults to a Graphic on the target if one exists and this is left empty.")]
    public Graphic colorTarget;

    public bool changeColorOnSelect = true;
    public Color selectedColor = new Color(1f, 0.85f, 0.3f);

    [Header("Timing")]
    [Tooltip("Seconds to animate in/out of the selected state.")]
    [Range(0.05f, 0.6f)]
    public float duration = 0.12f;

    Vector3 baseScale = Vector3.one;
    Color baseColor = Color.white;
    Coroutine scaleTween;
    Coroutine colorTween;

    void Awake()
    {
        if (target == null) target = transform as RectTransform;
        if (target != null) baseScale = target.localScale;

        if (colorTarget == null && target != null)
            colorTarget = target.GetComponent<Graphic>();

        if (colorTarget != null) baseColor = colorTarget.color;
    }

    public void OnSelect(BaseEventData eventData)
    {
        AnimateScaleTo(selectedScale);
        if (changeColorOnSelect) AnimateColorTo(selectedColor);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        AnimateScaleTo(1f);
        if (changeColorOnSelect) AnimateColorTo(baseColor);
    }

    void AnimateScaleTo(float scaleMultiplier)
    {
        if (target == null) return;
        if (scaleTween != null) StopCoroutine(scaleTween);
        scaleTween = StartCoroutine(ScaleRoutine(baseScale * scaleMultiplier));
    }

    void AnimateColorTo(Color to)
    {
        if (colorTarget == null) return;
        if (colorTween != null) StopCoroutine(colorTween);
        colorTween = StartCoroutine(ColorRoutine(to));
    }

    IEnumerator ScaleRoutine(Vector3 to)
    {
        Vector3 from = target.localScale;
        float t = 0f;

        if (duration <= 0f) { target.localScale = to; yield break; }

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            target.localScale = Vector3.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        target.localScale = to;
    }

    IEnumerator ColorRoutine(Color to)
    {
        Color from = colorTarget.color;
        float t = 0f;

        if (duration <= 0f) { colorTarget.color = to; yield break; }

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            colorTarget.color = Color.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        colorTarget.color = to;
    }
}
