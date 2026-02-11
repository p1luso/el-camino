using UnityEngine;
using Mapbox.Unity.Map;
using Mapbox.Unity.MeshGeneration.Data;
using Mapbox.Unity.Map.TileProviders;
using System.Collections.Generic;
using ElCamino.Core; // Added for POIManager

namespace ElCamino.Map
{
    /// <summary>
    /// Automatically applies Neon/Cyberpunk style materials to Mapbox features
    /// generated at runtime.
    /// </summary>
    public class NeonCityStyler : MonoBehaviour
    {
        [Header("Settings")]
        public bool applyToBuildings = true;
        public bool applyToRoads = true;
        public string playerTag = "Player";

        [Header("Colors - Buildings")]
        public Color buildingMainColor = new Color(0.0f, 1.0f, 1.0f, 0.4f); // Cyan Translucent Base (Solid Color)
        public Color buildingRimColor = new Color(0.8f, 1.0f, 1.0f, 1f); // White/Cyan Edge

        [Header("Colors - Roads")]
        public Color roadMainColor = new Color(1.0f, 0.9f, 0.0f, 0.5f); // Yellow Translucent
        public Color roadLineColor = new Color(1.0f, 1.0f, 0.5f, 1f); // Bright Yellow Edge

        [Header("Colors - Landuse")]
        public Color landuseMainColor = new Color(0.0f, 0.05f, 0.2f, 0.9f); // Dark Blue Opaque-ish

        private AbstractMap _map;
        private Material _neonBuildingMat;
        private Material _neonRoadMat;
        private Material _neonLanduseMat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInitialize()
        {
            // Check if already exists
            if (FindFirstObjectByType<NeonCityStyler>() != null) return;

            GameObject go = new GameObject("NeonCityStyler");
            go.AddComponent<NeonCityStyler>();
            DontDestroyOnLoad(go);
            Debug.Log("[NeonCityStyler] Automatically created to apply Cyberpunk styles.");
        }

        void Start()
        {
            _map = FindFirstObjectByType<AbstractMap>();
            if (_map == null)
            {
                Debug.LogError("[NeonCityStyler] No AbstractMap found!");
                return;
            }

            CreateMaterials();
            SetupLighting();

            _map.OnTileFinished += OnTileFinished;
            
            // --- FIX: Apply to already existing tiles ---
            // Often NeonCityStyler starts AFTER some tiles are already created.
            // We need to find them and style them immediately.
            var existingTiles = FindObjectsByType<UnityTile>(FindObjectsSortMode.None);
            foreach(var tile in existingTiles)
            {
                OnTileFinished(tile);
            }
            // --------------------------------------------
            
            // Fix Player Lighting
            FixPlayerLighting();

            // Configure Map Extent and Fog
            ConfigureMapAndFog();
            
            // Spawn AI Companion if needed
            SpawnAICompanion();
        }

        void SpawnAICompanion()
        {
            if (GameObject.FindObjectOfType<ElCamino.AI.AICompanionController>() == null)
            {
                GameObject companion = new GameObject("AI_Companion");
                companion.AddComponent<ElCamino.AI.AICompanionController>();
                // Position near player
                GameObject player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null)
                {
                    companion.transform.position = player.transform.position + Vector3.up * 2f;
                }
            }
        }

        void ConfigureMapAndFog()
        {
            // 1. Fog Setup (Cyberpunk Blur)
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.0f, 0.0f, 0.1f); // Dark Blue / Black
            RenderSettings.fogStartDistance = 400f; // Start fading at 400m
            RenderSettings.fogEndDistance = 1200f; // Fully obscured at 1.2km
            
            // Adjust Camera Clipping Plane if needed
            UnityEngine.Camera mainCam = UnityEngine.Camera.main;
            if (mainCam != null)
            {
                mainCam.backgroundColor = RenderSettings.fogColor;
                mainCam.clearFlags = CameraClearFlags.SolidColor;
                mainCam.farClipPlane = 1500f;
            }

            // 2. Map Extent Setup (Continuous Generation)
            if (_map != null)
            {
                // Ensure we use RangeAroundTransform
                GameObject player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null)
                {
                    // Access the specific options for RangeAroundTransform in the MapOptions
                    // We must set this BEFORE calling SetExtent because SetExtent triggers initialization immediately
                    // and the Mapbox implementation of SetExtent ignores the passed options parameter due to a bug/design.
                    var extentOptions = _map.Options.extentOptions.defaultExtents.rangeAroundTransformOptions;
                    extentOptions.targetTransform = player.transform;
                    extentOptions.visibleBuffer = 1; // Radius of 1 tile (Minimal for performance)
                    extentOptions.disposeBuffer = 1; // Dispose immediately
                    
                    // Now set the extent type. This will use the options we just configured.
                    _map.SetExtent(MapExtentType.RangeAroundTransform);
                    Debug.Log("[NeonCityStyler] Map Extent set to RangeAroundTransform (Player) - Minimal Range.");
                }
                else
                {
                    Debug.LogWarning("[NeonCityStyler] Player not found for Map Extent configuration.");
                }
            }
        }

        public void FixPlayerLighting()
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player == null) return;

            // 1. Force Unlit / Self-Illuminated Materials
            Renderer[] renderers = player.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                // Disable Shadow Casting/Receiving
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;

                foreach (var mat in r.materials)
                {
                    // Option A: Switch shader to Unlit
                    // mat.shader = Shader.Find("Universal Render Pipeline/Unlit"); 
                    // (Might lose textures if property names mismatch)

                    // Option B: Boost Emission (Safe bet)
                    mat.EnableKeyword("_EMISSION");
                    
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        Color baseColor = Color.white;
                        if (mat.HasProperty("_BaseColor")) baseColor = mat.GetColor("_BaseColor");
                        else if (mat.HasProperty("_Color")) baseColor = mat.GetColor("_Color");

                        // Make it glow slightly with its own color (reduced intensity)
                        mat.SetColor("_EmissionColor", baseColor * 0.25f); 
                    }

                    // Option C: REMOVED - Do not force white color, preserve original texture/tint
                    // if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                    // if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                }
            }
            
            // 2. Add a dedicated Point Light attached to player (if not exists)
            Light playerLight = player.GetComponentInChildren<Light>();
            if (playerLight == null)
            {
                GameObject lightObj = new GameObject("PlayerAuraLight");
                lightObj.transform.SetParent(player.transform);
                lightObj.transform.localPosition = Vector3.up * 2f;
                playerLight = lightObj.AddComponent<Light>();
                playerLight.type = LightType.Point;
                playerLight.range = 20f;
                playerLight.intensity = 2f;
                playerLight.color = Color.white;
                playerLight.shadows = LightShadows.None;
            }
        }

        void SetupLighting()
        {
            // Set a dark mood for Neon to pop
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            // Slightly brighter ambient to ensure buildings aren't pitch black
            RenderSettings.ambientLight = new Color(0.1f, 0.1f, 0.2f); 
            
            // Revert to Standard Neon Fog (Exponential) - Lighter and smoother
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.05f, 0.0f, 0.1f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.01f; // Slightly denser to hide the closer edge
            
            // Optional: If there is a Directional Light, dim it or color it
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    l.color = new Color(0.1f, 0.1f, 0.3f); // Moonlight/Neon tint
                    l.intensity = 0.3f; // Dimmer to let emission shine
                }
            }
        }

        void OnDestroy()
        {
            if (_map != null)
            {
                _map.OnTileFinished -= OnTileFinished;
            }
        }

        void CreateMaterials()
        {
            // Building Material
            // Use Resources.Load if Shader.Find fails for non-included shaders
            Shader buildingShader = Shader.Find("Custom/NeonBuilding");
            if (buildingShader == null) buildingShader = Resources.Load<Shader>("Shaders/NeonBuilding");

            if (buildingShader != null)
            {
                _neonBuildingMat = new Material(buildingShader);
                _neonBuildingMat.SetColor("_MainColor", buildingMainColor);
                _neonBuildingMat.SetColor("_RimColor", buildingRimColor);
                _neonBuildingMat.SetFloat("_RimPower", 3.0f);
                _neonBuildingMat.SetFloat("_EmissionGain", 1.5f);
            }
            else
            {
                Debug.LogWarning("[NeonCityStyler] Shader 'Custom/NeonBuilding' not found. Make sure it is in Resources/Shaders.");
            }

            // Road Material
            // We use the new StencilRoad shader to write to stencil buffer for trail clipping
            Shader roadShader = Shader.Find("Custom/StencilRoad");
            if (roadShader == null) roadShader = Resources.Load<Shader>("Shaders/StencilRoad");

            if (roadShader != null)
            {
                _neonRoadMat = new Material(roadShader);
                _neonRoadMat.SetColor("_MainColor", roadMainColor);
                // StencilRoad doesn't support EdgeColor/GlowIntensity in the simple version, 
                // but we keep the main color consistent.
            }
            else
            {
                Debug.LogWarning("[NeonCityStyler] Shader 'Custom/StencilRoad' not found. Make sure it is in Resources/Shaders.");
            }

            // Landuse Material
            Shader landuseShader = Shader.Find("Custom/NeonLanduse");
            if (landuseShader == null) landuseShader = Resources.Load<Shader>("Shaders/NeonLanduse");

            if (landuseShader != null)
            {
                _neonLanduseMat = new Material(landuseShader);
                _neonLanduseMat.SetColor("_MainColor", landuseMainColor);
            }
        }

        void OnTileFinished(Mapbox.Unity.MeshGeneration.Data.UnityTile tile)
        {
            // Iterate recursively to find renderers
            ProcessObject(tile.gameObject);
        }

        void ProcessObject(GameObject obj)
        {
            // Check if Road
            if (applyToRoads && (obj.GetComponent<RoadSegment>() != null || obj.name.ToLower().Contains("road") || obj.name.ToLower().Contains("way")))
            {
                // Debug.Log($"[NeonCityStyler] Applying Road Stencil to: {obj.name}");
                ApplyMaterial(obj, _neonRoadMat);
            }
            // Check if Building (simple heuristic: name contains building or extrusions)
            else if (applyToBuildings && (obj.name.ToLower().Contains("building") || obj.name.ToLower().Contains("extruded")))
            {
                ApplyMaterial(obj, _neonBuildingMat);
            }
            // Check if Landuse (parks, etc.) or just "Terrain"
            // Adding more keywords to catch the "pink" stuff which is likely "landuse", "region", "earth", or default mesh
            else if (obj.name.ToLower().Contains("landuse") || 
                     obj.name.ToLower().Contains("park") || 
                     obj.name.ToLower().Contains("green") || 
                     obj.name.ToLower().Contains("poi") ||
                     obj.name.ToLower().Contains("region") ||
                     obj.name.ToLower().Contains("earth"))
            {
                ApplyMaterial(obj, _neonLanduseMat);
                
                // --- POI REGISTRATION ---
                if (POIManager.Instance != null)
                {
                    string n = obj.name.ToLower();
                    POIManager.POIType type = POIManager.POIType.Generic;
                    
                    if (n.Contains("park") || n.Contains("green")) type = POIManager.POIType.Park;
                    else if (n.Contains("school") || n.Contains("university")) type = POIManager.POIType.School;
                    else if (n.Contains("hospital") || n.Contains("clinic")) type = POIManager.POIType.Hospital;
                    else if (n.Contains("fuel") || n.Contains("gas")) type = POIManager.POIType.GasStation;
                    else if (n.Contains("restaurant") || n.Contains("food")) type = POIManager.POIType.Food;
                    
                    POIManager.Instance.RegisterPOI(obj, type);
                }
            }
            // Explicitly check for UnityTile component (the main floor tile)
            else if (obj.GetComponent<UnityTile>() != null)
            {
                //Debug.Log($"[NeonCityStyler] Found UnityTile (Floor): {obj.name}. Applying Dark Blue Material.");
                ApplyMaterial(obj, _neonLanduseMat);
                
                // Ensure the renderer is enabled and settings are correct for a floor
                Renderer r = obj.GetComponent<Renderer>();
                if (r != null)
                {
                    r.enabled = true;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // Floor doesn't cast shadows
                    r.receiveShadows = true; // Floor receives shadows
                }
            }

            // Recurse
            foreach (Transform child in obj.transform)
            {
                ProcessObject(child.gameObject);
            }
        }

        void ApplyMaterial(GameObject obj, Material mat)
        {
            if (mat == null) return;

            var renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Apply to ALL material slots (Roof + Facades)
                Material[] newMaterials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < newMaterials.Length; i++)
                {
                    newMaterials[i] = mat;
                }
                renderer.sharedMaterials = newMaterials;
            }
        }
    }
}
