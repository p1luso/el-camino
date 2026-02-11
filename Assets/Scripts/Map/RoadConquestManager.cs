using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class RoadConquestManager : MonoBehaviour
{
    public static RoadConquestManager Instance { get; private set; }

    private HashSet<ulong> _conqueredRoadIds = new HashSet<ulong>();
    private string _savePath;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _savePath = Path.Combine(Application.persistentDataPath, "conquered_roads.json");
            LoadConquestData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public bool IsRoadConquered(ulong roadId)
    {
        return _conqueredRoadIds.Contains(roadId);
    }

    public void RegisterConquest(ulong roadId)
    {
        if (!_conqueredRoadIds.Contains(roadId))
        {
            _conqueredRoadIds.Add(roadId);
            SaveConquestData();
            // Optional: Play a sound or effect here
        }
    }

    private void SaveConquestData()
    {
        ConquestData data = new ConquestData();
        data.ids = new List<ulong>(_conqueredRoadIds);
        string json = JsonUtility.ToJson(data);
        File.WriteAllText(_savePath, json);
    }

    private void LoadConquestData()
    {
        if (File.Exists(_savePath))
        {
            try
            {
                string json = File.ReadAllText(_savePath);
                ConquestData data = JsonUtility.FromJson<ConquestData>(json);
                if (data != null && data.ids != null)
                {
                    _conqueredRoadIds = new HashSet<ulong>(data.ids);
                    Debug.Log($"[RoadConquestManager] Loaded {_conqueredRoadIds.Count} conquered roads.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RoadConquestManager] Error loading data: {e.Message}");
            }
        }
    }

    [System.Serializable]
    private class ConquestData
    {
        public List<ulong> ids;
    }
}
