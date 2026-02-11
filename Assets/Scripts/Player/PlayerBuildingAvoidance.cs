using UnityEngine;

public class PlayerBuildingAvoidance : MonoBehaviour
{
    [Header("Settings")]
    public LayerMask buildingLayer;
    public float playerRadius = 1.0f;
    public float pushBuffer = 0.5f;
    public float correctionSpeed = 5f; // Lower speed for smoother adjustments

    private Vector3 _avoidanceOffset = Vector3.zero;

    void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    // Removed LateUpdate. Now controlled by PlayerMovement.
    // void LateUpdate() { ... }

    /// <summary>
    /// Calculates the avoidance offset based on the base position (GPS/Road).
    /// Updates the internal offset state smoothly.
    /// </summary>
    public Vector3 GetAvoidanceOffset(Vector3 basePosition)
    {
        // 1. Where would we be with the current offset?
        Vector3 currentCheckPos = basePosition + _avoidanceOffset;

        // 2. Check for collisions at this proposed position
        Collider[] hitColliders = Physics.OverlapSphere(currentCheckPos, playerRadius, buildingLayer);

        if (hitColliders.Length > 0)
        {
            Vector3 bestExitPoint = currentCheckPos;
            float shortestDistance = float.MaxValue;
            bool foundExit = false;

            foreach (var col in hitColliders)
            {
                // Safety Check: Ignore Roads if they ended up in the Building Layer mask
                // (Common mistake: LayerMask includes Default, and Roads are on Default)
                if (col.GetComponent<RoadSegment>() != null || col.gameObject.name.ToLower().Contains("road"))
                {
                    continue;
                }
                
                // Also ignore terrain/ground if possible (usually huge bounds)
                if (col is TerrainCollider) continue;

                Vector3 closestPointOnBounds = col.ClosestPoint(currentCheckPos);
                float dist = Vector3.Distance(currentCheckPos, closestPointOnBounds);
                
                // Direction OUT of the building
                Vector3 pushDirection = (closestPointOnBounds - col.bounds.center).normalized;
                pushDirection.y = 0; 
                pushDirection.Normalize();
                if (pushDirection == Vector3.zero) pushDirection = Vector3.forward;

                Vector3 potentialExit = closestPointOnBounds + (pushDirection * pushBuffer);

                if (dist < shortestDistance)
                {
                    shortestDistance = dist;
                    bestExitPoint = potentialExit;
                    foundExit = true;
                }
            }

            if (foundExit)
            {
                Vector3 targetPos = bestExitPoint;
                targetPos.y = currentCheckPos.y; // Lock height to input height

                // The correction needed to get from currentCheckPos to targetPos
                Vector3 correction = targetPos - currentCheckPos;

                // Update persistent offset
                Vector3 newOffsetTarget = _avoidanceOffset + correction;
                _avoidanceOffset = Vector3.Lerp(_avoidanceOffset, newOffsetTarget, Time.deltaTime * correctionSpeed);
            }
        }
        else
        {
            // 3. Decay offset if safe
            _avoidanceOffset = Vector3.Lerp(_avoidanceOffset, Vector3.zero, Time.deltaTime * 1.0f);
        }

        return _avoidanceOffset;
    }

    public bool IsColliding(Vector3 pos)
    {
        return Physics.CheckSphere(pos, playerRadius, buildingLayer);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, playerRadius);
    }
}