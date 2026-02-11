using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class RoadSegment : MonoBehaviour
{
    [Header("Configuration")]
    public Material visitedMaterial;
    public string playerTag = "Player";
    [Tooltip("Margen de detección en metros alrededor de la calle")]
    public float detectionMargin = 3f;
    
    // ID único de Mapbox para persistencia
    public ulong RoadId; 

    private MeshRenderer _renderer;
    private bool _isVisited = false;
    private Material[] _originalMaterials;
    private Transform _playerTransform;
    private static Material _fallbackMat;

    void Start()
    {
        _renderer = GetComponent<MeshRenderer>();
        if (_renderer != null)
        {
            _originalMaterials = _renderer.sharedMaterials;
        }

        // Check if already conquered
        // if (RoadConquestManager.Instance != null && RoadConquestManager.Instance.IsRoadConquered(RoadId))
        // {
        //    SetMaterialVisited();
        //    _isVisited = true;
        // }

        var playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;

            // --- FIX: Ignore Physical Collision with Player ---
            // The roads have MeshColliders for raycasting (snapping), but we don't want the player 
            // (who has Rigidbody + BoxCollider) to trip over them or fly.
            var roadCollider = GetComponent<Collider>();
            var playerColliders = playerObj.GetComponentsInChildren<Collider>();

            if (roadCollider != null && playerColliders != null)
            {
                foreach (var pc in playerColliders)
                {
                    Physics.IgnoreCollision(roadCollider, pc, true);
                }
            }
        }
    }

    void Update()
    {
        // Ya no necesitamos lógica de actualización ni detección de proximidad
        // El rastro (LineRenderer) se encarga de pintar exactamente por donde pasamos.
        // Este script queda solo como marcador o para lógica futura.
    }

    // Método llamado por PlayerRoadSnapper o RoadConqueror
    public void MarkAsVisited()
    {
        if (_isVisited) return;
        
        _isVisited = true;
        // SetMaterialVisited(); // DISABLE WHOLE ROAD PAINTING

        if (RoadConquestManager.Instance != null)
        {
            RoadConquestManager.Instance.RegisterConquest(RoadId);
        }
    }

    private void SetMaterialVisited()
    {
        if (_renderer != null)
        {
            Material matToUse = visitedMaterial;
            
            // Fallback if not assigned
            if (matToUse == null)
            {
                if (_fallbackMat == null)
                {
                     // Try to find Neon Road Shader or use Unlit
                     Shader s = Shader.Find("Custom/NeonRoad");
                     if (s == null) s = Shader.Find("Unlit/Color");
                     
                     _fallbackMat = new Material(s);
                     if (_fallbackMat.HasProperty("_MainColor")) _fallbackMat.SetColor("_MainColor", new Color(0f, 1f, 0f, 0.8f)); // Neon Green
                     else _fallbackMat.color = new Color(0f, 1f, 0f);
                }
                matToUse = _fallbackMat;
            }

            // Apply to all slots
            Material[] mats = new Material[_renderer.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = matToUse;
            }
            _renderer.sharedMaterials = mats;
        }
    }
}
