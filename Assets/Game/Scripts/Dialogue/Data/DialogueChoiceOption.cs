using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One selectable response on a <see cref="DialogueChoiceNode"/>.
/// Step 3 shows every authored option. <see cref="requiredCharacterId"/>,
/// <see cref="availability"/>, and <see cref="textVariants"/> are stored for Step 5
/// and are not evaluated yet.
/// </summary>
[Serializable]
public class DialogueChoiceOption
{
    public string optionId = "";

    [TextArea(1, 4)]
    [Tooltip("Default label. Step 5 will apply textVariants.")]
    public string text = "";

    [Tooltip("Step 5. Not applied in Step 3.")]
    public List<DialogueTextVariant> textVariants = new List<DialogueTextVariant>();

    [Tooltip("Next node id. Empty = end the conversation (Completed).")]
    public string nextNodeId = "";

    [Tooltip("Step 5. Not evaluated in Step 3.")]
    public string requiredCharacterId = "";

    [SerializeReference]
    [Tooltip("Step 5. Not evaluated in Step 3. All authored options stay selectable.")]
    public List<EventCondition> availability = new List<EventCondition>();
}
