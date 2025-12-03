// DialogueDatabase.cs
using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/DialogueDatabase", fileName = "DialogueDatabase")]
public class DialogueDatabase : ScriptableObject
{
    // Editor에서 모든 DialogueAsset을 끌어다 놓거나, Importer가 자동으로 채워넣을 수 있게 만듭니다.
    public DialogueAsset[] dialogueAssets;
}