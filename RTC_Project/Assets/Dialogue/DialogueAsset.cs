using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/DialogueAsset", fileName = "DialogueAsset")]
public class DialogueAsset : ScriptableObject 
{
    public string speaker;
    public string id;
    public string condition;
    [TextArea] public string text;
    public string duration;
    public string nextId;
    public string note;
}
