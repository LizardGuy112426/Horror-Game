using System;
using UnityEngine;

[Serializable]
public sealed class DialogueLine
{
    [Tooltip("Name shown in the dialogue name box.")]
    public string speakerName;

    [TextArea(3, 8)]
    [Tooltip("Dialogue revealed one character at a time.")]
    public string dialogue;
}
