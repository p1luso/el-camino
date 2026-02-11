using UnityEngine;
using UnityEditor;
using System.IO;

public class SceneMover
{
    [MenuItem("Mapbox/Move Scene and Fix Build")]
    public static void MoveScene()
    {
        string sourcePath = "Assets/Mapbox/Examples/0_PrefabScenes/Location-basedGame.unity";
        string destPath = "Assets/Scenes/MainGame.unity";

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        string error = AssetDatabase.MoveAsset(sourcePath, destPath);
        
        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError("Error moving scene: " + error);
        }
        else
        {
            Debug.Log("Scene moved to " + destPath);
            
            // Update Build Settings
            EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(destPath, true)
            };
            
            EditorBuildSettings.scenes = scenes;
            Debug.Log("Build Settings updated to use " + destPath);
        }
    }
}
