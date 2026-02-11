using UnityEngine;
using Mapbox.Unity.Map;

namespace ElCamino.Map
{
    public class SetLayerOnMapFeatures : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Name of the Unity Layer to assign (e.g. 'Buildings' or 'Roads')")]
        public string targetLayerName = "Default";

        [Tooltip("Only apply to tiles/features containing this string in their name (leave empty to apply to everything)")]
        public string nameFilter = "";

        private AbstractMap _map;

        void Awake()
        {
            _map = GetComponent<AbstractMap>();
            if (_map != null)
            {
                _map.OnTileFinished += OnTileFinished;
            }
        }

        void OnTileFinished(Mapbox.Unity.MeshGeneration.Data.UnityTile tile)
        {
            int layerID = LayerMask.NameToLayer(targetLayerName);
            if (layerID == -1)
            {
                // Only log warning once to avoid spam
                return; 
            }

            foreach (Transform child in tile.transform)
            {
                // Mapbox usually groups features under objects named like "building", "road", etc.
                // Check if this child matches our filter (supports comma separated list, e.g. "road,street,untitled")
                if (string.IsNullOrEmpty(nameFilter))
                {
                     SetLayerRecursively(child.gameObject, layerID);
                }
                else
                {
                    string[] filters = nameFilter.ToLower().Split(',');
                    string childName = child.name.ToLower();
                    bool match = false;
                    
                    foreach (var f in filters)
                    {
                        if (!string.IsNullOrWhiteSpace(f) && childName.Contains(f.Trim()))
                        {
                            match = true;
                            break;
                        }
                    }

                    if (match)
                    {
                        SetLayerRecursively(child.gameObject, layerID);
                    }
                }
            }
        }

        void SetLayerRecursively(GameObject obj, int newLayer)
        {
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }
    }
}