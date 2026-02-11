using UnityEngine;
using Mapbox.Unity.Map;
using Mapbox.Unity.Location;
using Mapbox.Utils;
using System.Collections.Generic;
using System.IO;

[RequireComponent(typeof(LineRenderer))]
public class TrailRecorder : MonoBehaviour
{
    [Header("Map Reference")]
    public AbstractMap map;

    [Header("Visual Settings")]
    public Color trailColor = new Color(0.0f, 1.0f, 0.0f, 1f); // Neon Green
    
    // Removed Splat settings
    public float lineWidth = 14.0f; // Much Wider to cover the whole street width

    [Header("Recording Settings")]
    [Tooltip("Distancia mínima en metros para registrar un nuevo punto")]
    public float minDistance = 0.5f; // Very sensitive to start painting immediately

    private LineRenderer _lineRenderer;
    private TrailData _trailData;
    private ILocationProvider _locationProvider;
    private string _savePath;
    private Vector2d _lastAddedLocation;
    private bool _isInitialized;
    
    // Container for splats to keep hierarchy clean
    // private Transform _splatContainer;

    void Start()
    {
        // 1. Configurar ruta de guardado
        _savePath = Path.Combine(Application.persistentDataPath, "player_trail.json");
        
        // 2. Setup LineRenderer
        _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer == null) _lineRenderer = gameObject.AddComponent<LineRenderer>();
        
        // Ensure container follows map if map moves (though usually map root is static and world moves)
        // If Mapbox AbstractMap is the root, we parent to it.
        if (map == null) map = FindFirstObjectByType<AbstractMap>();
        
        // Safety Check
        if (map == null)
        {
            Debug.LogError("[TrailRecorder] AbstractMap Reference is MISSING! Please assign it in the Inspector.");
        }
        else
        {
            // Parent this object to map to keep coordinates consistent if map root moves
            transform.SetParent(map.transform);
        }

        // Configure LineRenderer
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.alignment = LineAlignment.TransformZ; // Lie flat
        _lineRenderer.textureMode = LineTextureMode.Tile;
        _lineRenderer.startWidth = lineWidth;
        _lineRenderer.endWidth = lineWidth;
        _lineRenderer.numCornerVertices = 4; // Smooth corners
        _lineRenderer.numCapVertices = 4; // Smooth ends
        _lineRenderer.startColor = trailColor; // Ensure Vertex Color is set
        _lineRenderer.endColor = trailColor;
        
        // Material
        Shader s = Shader.Find("Custom/StencilTrail");
        if (s == null) s = Resources.Load<Shader>("Shaders/StencilTrail");
        if (s == null) s = Shader.Find("Sprites/Default");
        
        Material mat = new Material(s);
        mat.SetColor("_MainColor", trailColor);
        mat.SetFloat("_GlowIntensity", 2.0f);
        _lineRenderer.material = mat;

        // 3. Cargar datos existentes
        LoadTrail();

        // FIX: Subscribe to Map Initialized event
        if (map != null)
        {
            map.OnInitialized += OnMapInitialized;
        }

        // 5. Inicializar listener de ubicación
        Invoke("InitializeLocationProvider", 0.5f);
    }
    
    // Helper to create default splat if prefab is missing (REMOVED)
    // private GameObject CreateDefaultSplat() ...

    void OnMapInitialized()
    {
        Debug.Log("[TrailRecorder] Map Initialized. Refreshing Visuals.");
        UpdateVisuals();
    }
    
    // LineRenderer refresh removed as we use Splats now
    // System.Collections.IEnumerator ForceTrailRefresh() ...

    void InitializeLocationProvider()
    {
        if (LocationProviderFactory.Instance != null)
        {
            _locationProvider = LocationProviderFactory.Instance.DefaultLocationProvider;
            if (_locationProvider != null)
            {
                _locationProvider.OnLocationUpdated += HandleLocationUpdated;
                _isInitialized = true;
            }
        }
        else
        {
            Debug.LogWarning("[TrailRecorder] LocationProviderFactory.Instance es null. Reintentando...");
            Invoke("InitializeLocationProvider", 1.0f);
        }
    }

    void OnDestroy()
    {
        if (_locationProvider != null)
        {
            _locationProvider.OnLocationUpdated -= HandleLocationUpdated;
        }
    }

    void HandleLocationUpdated(Location location)
    {
        if (map == null || !_isInitialized) return;

        var currentLatLon = location.LatitudeLongitude;
        Vector3 currentWorld = map.GeoToWorldPosition(currentLatLon);
        
        // --- Snap to Road Logic ---
        // Buscamos colliders en la capa de calles (si tienen) o por componente RoadSegment
        // Radio de búsqueda generoso (20 metros) para encontrar la calle más cercana
        Collider[] hits = Physics.OverlapSphere(currentWorld, 20.0f);
        float closestDistance = float.MaxValue;
        Vector3 bestPoint = currentWorld;
        bool foundRoad = false;

        foreach (var hit in hits)
        {
            // Verificamos si es una calle
            // RoadSegment está en el namespace global
            if (hit.GetComponent<RoadSegment>() != null || hit.gameObject.name.Contains("road"))
            {
                // Encontramos el punto más cercano en este collider
                // Fix: Custom implementation for non-convex MeshCollider
                Vector3 closestOnCollider = GetClosestPointOnMesh(hit, currentWorld);
                float dist = Vector3.Distance(currentWorld, closestOnCollider);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    bestPoint = closestOnCollider;
                    foundRoad = true;
                }
            }
        }

        if (foundRoad)
        {
            // Convertimos el punto de mundo "snappeado" de vuelta a GeoCoordinate
            // para guardarlo consistentemente (y que funcione si el mapa se recentra)
            currentLatLon = map.WorldToGeoPosition(bestPoint);
        }
        else
        {
            // If snapping failed (e.g. Combine Meshes hides individual colliders), 
            // try to use Player's visual position as fallback
            TrySnapToPlayer(ref currentLatLon, ref currentWorld);
            
            // Si aún así decidimos no pintar sin calle, return.
            // Pero si usamos PlayerPos, asumimos que es válido.
            // return; 
        }
        // --------------------------

        // Si no hay puntos, agregamos el primero
        if (_trailData.points.Count == 0)
        {
            AddPoint(currentLatLon);
            return;
        }

        // Convertir a posición mundo para medir distancia en metros (aprox)
        Vector3 lastWorld = map.GeoToWorldPosition(_lastAddedLocation);
        // Recalculamos currentWorld con la nueva latLon snappeada
        currentWorld = map.GeoToWorldPosition(currentLatLon); 
        
        // Solo guardar si nos movimos lo suficiente
        if (Vector3.Distance(lastWorld, currentWorld) >= minDistance)
        {
            AddPoint(currentLatLon);
        }
        
        // Actualizar visuales siempre (por si el mapa se recentra)
        // UpdateVisuals();
    }
    
    // Fallback: If map is null or snapping fails, use Player position if close enough
    void TrySnapToPlayer(ref Vector2d latLon, ref Vector3 worldPos)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
             Vector3 playerPos = player.transform.position;
             // Check if player is close to the GPS point (within reasonable GPS error, e.g., 50m)
             float dist = Vector3.Distance(playerPos, worldPos);
             if (dist < 50.0f)
             {
                 // Use player's visual position (which is already road-snapped)
                 // but keep Y slightly higher for trail
                 worldPos = playerPos;
                 latLon = map.WorldToGeoPosition(worldPos);
             }
        }
    }
    
    void LateUpdate()
    {
        // Solo refrescar si el mapa cambió de origen (detectar cambio masivo?)
        // Por ahora, no spamear UpdateVisuals en LateUpdate
        /*
         if (_trailData.points.Count > 0 && map != null)
         {
             UpdateVisuals();
         }
        */
    }

    void AddPoint(Vector2d latLon)
    {
        _trailData.points.Add(new TrailPoint(latLon));
        _lastAddedLocation = latLon;
        SaveTrail();
        UpdateVisuals(); 
    }

    /// <summary>
    /// Encuentra el punto más cercano en un MeshCollider no convexo.
    /// Iterando sobre los vértices (aproximación rápida) o triángulos (más precisa).
    /// </summary>
    Vector3 GetClosestPointOnMesh(Collider collider, Vector3 point)
    {
        MeshCollider meshCollider = collider as MeshCollider;
        if (meshCollider == null || meshCollider.sharedMesh == null)
        {
            return collider.ClosestPoint(point); // Fallback para Box/Sphere/Capsule
        }

        // Optimización: Transformamos el punto al espacio local una sola vez
        Vector3 localPoint = collider.transform.InverseTransformPoint(point);
        
        // Safety check for empty mesh
        if (meshCollider.sharedMesh.vertexCount == 0) return collider.ClosestPoint(point);

        Vector3[] vertices = meshCollider.sharedMesh.vertices;
        float minSqrDistance = float.MaxValue;
        Vector3 closestVertex = Vector3.zero;

        // Búsqueda simple por vértices (suficiente para "Snap to Road" si la malla está teselada)
        for (int i = 0; i < vertices.Length; i++)
        {
            float sqrDist = (vertices[i] - localPoint).sqrMagnitude;
            if (sqrDist < minSqrDistance)
            {
                minSqrDistance = sqrDist;
                closestVertex = vertices[i];
            }
        }

        return collider.transform.TransformPoint(closestVertex);
    }

    // Update Visuals Logic
        void UpdateVisuals()
        {
            if (map == null || _lineRenderer == null) return;

            // Debug Log to ensure we are trying to draw
            // if (_trailData.points.Count % 10 == 0) Debug.Log($"[TrailRecorder] Drawing {_trailData.points.Count} points.");

            _lineRenderer.positionCount = _trailData.points.Count;
        for (int i = 0; i < _trailData.points.Count; i++)
        {
            Vector3 worldPos = map.GeoToWorldPosition(_trailData.points[i].ToVector2d());
            
            // Adjust height: Higher than road (0), but not floating too high
            // StencilTrail clips to road, so we can be slightly above or at same level
            // We lift slightly to avoid Z-fighting with road mesh
            worldPos.y += 0.02f; 
            
            _lineRenderer.SetPosition(i, worldPos);
        }
    }
    
    // Removed SpawnSplat and CreateDefaultSplat

    public void SaveTrail()
    {
        string json = JsonUtility.ToJson(_trailData, true);
        File.WriteAllText(_savePath, json);
        
        // TODO: Integration Point for Online Multiplayer
        // In the next iteration, instead of just saving to local JSON, we will send this 'json' payload 
        // (or the _trailData object) to our backend API/Database.
        // Example: APIManager.Instance.UploadPlayerTrail(_trailData);
        // This will allow other players to download and visualize this trail for territory disputes.
    }

    public void LoadTrail()
    {
        if (File.Exists(_savePath))
        {
            try 
            {
                string json = File.ReadAllText(_savePath);
                _trailData = JsonUtility.FromJson<TrailData>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error cargando trail: {e.Message}");
                _trailData = new TrailData();
            }
        }
        else
        {
            _trailData = new TrailData();
        }
        
        if(_trailData.points.Count > 0)
        {
             _lastAddedLocation = _trailData.points[_trailData.points.Count - 1].ToVector2d();
        }
    }

    [ContextMenu("Borrar Datos Guardados")]
    public void ClearTrailData()
    {
        string path = _savePath;
        if (string.IsNullOrEmpty(path))
        {
            path = Path.Combine(Application.persistentDataPath, "player_trail.json");
        }

        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[TrailRecorder] Datos borrados de: {path}");
        }
        else
        {
            Debug.LogWarning("[TrailRecorder] No se encontró archivo para borrar.");
        }

        if (_trailData != null)
        {
            _trailData.points.Clear();
        }
        
        if (_lineRenderer != null)
        {
            _lineRenderer.positionCount = 0;
        }

        // Cleanup splat container if it exists from previous version
        if (transform.Find("Trail_Splats_Container") != null)
        {
             Destroy(transform.Find("Trail_Splats_Container").gameObject);
        }
    }
}
