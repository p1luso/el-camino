using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ElCamino.Core
{
    public class POIManager : MonoBehaviour
    {
        public static POIManager Instance { get; private set; }

        public enum POIType
        {
            Generic,
            Park,
            School,
            Hospital,
            GasStation,
            Food
        }

        [System.Serializable]
        public class POIData
        {
            public string name;
            public POIType type;
            public Vector3 position;
            public GameObject gameObject;
        }

        private List<POIData> _pois = new List<POIData>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void RegisterPOI(GameObject obj, POIType type)
        {
            // Avoid duplicates
            if (_pois.Any(p => p.gameObject == obj)) return;

            POIData data = new POIData
            {
                name = obj.name,
                type = type,
                position = obj.transform.position,
                gameObject = obj
            };

            _pois.Add(data);
            // Debug.Log($"[POIManager] Registered {type}: {obj.name}");
        }

        public POIData GetNearestPOI(Vector3 position, float radius)
        {
            // Simple linear search (optimization: use spatial partition if list gets huge)
            POIData nearest = null;
            float minDst = radius * radius; // Compare squared distances

            for (int i = _pois.Count - 1; i >= 0; i--)
            {
                if (_pois[i].gameObject == null)
                {
                    _pois.RemoveAt(i);
                    continue;
                }

                float dstSqr = (position - _pois[i].position).sqrMagnitude;
                if (dstSqr < minDst)
                {
                    minDst = dstSqr;
                    nearest = _pois[i];
                }
            }

            return nearest;
        }

        public Transform GetNearestGasStation(Vector3 position)
        {
            POIData nearest = null;
            float minDst = float.MaxValue;

            foreach (var poi in _pois)
            {
                if (poi.type != POIType.GasStation) continue;
                if (poi.gameObject == null) continue;

                float dstSqr = (position - poi.position).sqrMagnitude;
                if (dstSqr < minDst)
                {
                    minDst = dstSqr;
                    nearest = poi;
                }
            }

            return nearest?.gameObject.transform;
        }
    }
}