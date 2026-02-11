using UnityEngine;
using System.Collections.Generic;

namespace ElCamino.Player
{
    public class PlayerRoadSnapper : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("LayerMask for Roads. Make sure your Mapbox roads are on this layer.")]
        public LayerMask roadLayer;
        
        [Tooltip("Radios de búsqueda escalonados (en metros). Si no encuentra calle en el primero, prueba el siguiente.")]
        public float[] searchRadii = new float[] { 25f, 50f, 100f };

        [Tooltip("How fast the car snaps to the road.")]
        public float snapSpeed = 10f;

        [Tooltip("If true, the car will NEVER leave the road. If GPS drifts into a building, car stays on closest road point.")]
        public bool strictMode = true;
        
        [Tooltip("Altura extra sobre la calle para evitar clipping")]
        public float heightOffset = 0.05f; // Reduced to be closer to road (was 1.0f)

        [Header("Debug")]
        public bool showDebugGizmos = true;
        public bool debugLogs = true;

        private Vector3 _targetPosition;
        private bool _isOnRoad = false;
        private Vector3 _lastValidRoadPosition;
        private bool _hasValidPosition = false;

        void Start()
        {
            if (roadLayer == 0 || roadLayer.value == 0)
            {
                Debug.LogError("PlayerRoadSnapper: Road Layer is NOT set properly! Value is 0. Please assign a Layer in Inspector.");
            }
            else
            {
                Debug.Log($"PlayerRoadSnapper: Initialized. Road Layer Value: {roadLayer.value}");
            }
            _targetPosition = transform.position;
            _lastValidRoadPosition = transform.position;
        }

        // Removed LateUpdate to prevent conflict with PlayerMovement
        // void LateUpdate() { SnapToRoad(); }

        /// <summary>
        /// Calculates the snapped position based on the input position.
        /// Returns the modified position if a road is found, or the original position if not.
        /// </summary>
        public Vector3 GetSnappedPosition(Vector3 currentPos, out bool snapped)
        {
            snapped = false;
            
            // 1. Check if we are ALREADY on a road (Raycast down)
            Ray ray = new Ray(currentPos + Vector3.up * 50f, Vector3.down);
            RaycastHit hit;

            // If we hit a road directly below us, we are good!
            if (Physics.Raycast(ray, out hit, 100f, roadLayer))
            {
                snapped = true;
                _isOnRoad = true;
                
                // Conquer Road
                var seg = hit.collider.GetComponent<RoadSegment>();
                if (seg != null) seg.MarkAsVisited();

                // Keep X/Z, snap Y to hit point + offset
                Vector3 result = new Vector3(currentPos.x, hit.point.y + heightOffset, currentPos.z);
                _lastValidRoadPosition = result;
                _hasValidPosition = true;
                return result;
            }

            _isOnRoad = false;

            // 2. We are NOT on a road. Find closest road using expanding radii.
            
            Collider bestRoad = null;
            Vector3 bestPoint = currentPos;
            float globalClosestDist = float.MaxValue;
            bool foundAny = false;

            // If Strict Mode, add a huge radius at the end to ensure we find SOMETHING
            List<float> radiiToCheck = new List<float>(searchRadii);
            if (strictMode) radiiToCheck.Add(500f);

            foreach (float radius in radiiToCheck)
            {
                Collider[] roads = Physics.OverlapSphere(currentPos, radius, roadLayer);
                
                if (roads.Length > 0)
                {
                    // if(debugLogs) Debug.Log($"PlayerRoadSnapper: Found {roads.Length} roads in {radius}m radius.");

                    foreach (var roadCol in roads)
                    {
                        // ClosestPoint gives us the point on the road mesh closest to our GPS position
                        Vector3 pointOnRoad = roadCol.ClosestPoint(currentPos);
                        
                        // Ignore Y difference for distance check (2D distance)
                        float dist = Vector2.Distance(new Vector2(currentPos.x, currentPos.z), new Vector2(pointOnRoad.x, pointOnRoad.z));
                        
                        if (dist < globalClosestDist)
                        {
                            globalClosestDist = dist;
                            bestPoint = pointOnRoad;
                            bestRoad = roadCol;
                            foundAny = true;
                        }
                    }

                    if (foundAny) break;
                }
            }

            if (foundAny)
            {
                // if(debugLogs) Debug.Log($"PlayerRoadSnapper: Snapping to {bestRoad.name}. Dist: {globalClosestDist}");
                
                snapped = true;
                Vector3 finalPos = bestPoint;
                finalPos.y += heightOffset;
                
                // Conquer Road
                var seg = bestRoad.GetComponent<RoadSegment>();
                if (seg != null) seg.MarkAsVisited();

                _lastValidRoadPosition = finalPos;
                _hasValidPosition = true;
                return finalPos;
            }

            // 3. Strict Mode Fallback
            if (strictMode && _hasValidPosition)
            {
                // If we didn't find a road but we have a previous valid one, STAY THERE.
                // Do not drift into buildings.
                snapped = true; // Pretend we snapped so we don't trigger avoidance
                return _lastValidRoadPosition;
            }

            // No road found, return original (only if strict mode is off or no history)
            return currentPos;
        }

        void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // Draw search radii
            Gizmos.color = Color.yellow;
            foreach(float r in searchRadii) Gizmos.DrawWireSphere(transform.position, r);

            if (_isOnRoad)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(transform.position + Vector3.up * 2, 1f);
            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 10);
            }
        }
    }
}