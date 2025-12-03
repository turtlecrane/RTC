// DialogueCsvImporter.cs (Editor)
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class DialogueCsvImporter : EditorWindow
{
    public TextAsset csvFile;
    [MenuItem("Tools/Dialogue/Import CSV")]
    public static void ShowWindow() => GetWindow<DialogueCsvImporter>("Dialogue Importer");

    private void OnGUI()
    {
        csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV File", csvFile, typeof(TextAsset), false);
        if (GUILayout.Button("Import"))
        {
            if (csvFile == null) return;
            Import(csvFile);
        }
    }

    private void Import(TextAsset csv)
    {
        string baseDir = "Assets/Dialogue/Runtime/ScriptableObjects";

        string[] lines = csv.text.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        var header = lines[0].Split(',');

        for (int i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split(',');
            string speaker = cols[0].Trim();
            string id = cols[1].Trim();
            string condition = cols[2].Trim();
            string text = cols[3].Trim();
            string duration = cols[4].Trim();
            string nextId = cols[5].Trim();
            string notes = cols[6].Trim();

            // 폴더 생성
            string folderPath = $"{baseDir}/{speaker}";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string parent = baseDir;
                string newFolderName = speaker;

                AssetDatabase.CreateFolder(parent, newFolderName);
            }

            // ScriptableObject 생성
            DialogueAsset asset = ScriptableObject.CreateInstance<DialogueAsset>();
            asset.speaker = speaker;
            asset.id = id;
            asset.condition = condition;
            asset.text = text;
            asset.duration = duration;
            asset.nextId = nextId;
            asset.note = notes;

            // 파일 경로
            string assetPath = $"{folderPath}/{speaker}_{id}.asset";

            AssetDatabase.CreateAsset(asset, assetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Import complete");
    }
}