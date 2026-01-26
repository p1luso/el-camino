using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Settings")]
    public float smoothSpeed = 5f;
    [Tooltip("Distance from the target")]
    public float distance = 40f;
    [Tooltip("Height angle in degrees (X rotation)")]
    public float pitchAngle = 75f;
    
    [Header("Offset Adjustment")]
    [Tooltip("Manual offset to center properly if needed")]
    public Vector3 manualOffset = Vector3.zero;

    private Vector3 _calculatedOffset;

    void Start()
    {
        // Calculate initial offset based on distance and pitch
        CalculateOffset();
    }

    void OnValidate()   
    {
        // Update offset in editor when changing values
        CalculateOffset();
    }

    void CalculateOffset()
    {
        // Basic trigonometry to place camera behind and up relative to a flat plane
        // pitchAngle 90 = top down, 0 = horizontal
        float radians = pitchAngle * Mathf.Deg2Rad;
        
        // At 90 deg: Z offset should be 0, Y offset should be distance
        // At 0 deg: Z offset should be -distance, Y offset should be 0
        // We use -Z because camera is behind
        
        float zOffset = -Mathf.Cos(radians) * distance;
        float yOffset = Mathf.Sin(radians) * distance;

        _calculatedOffset = new Vector3(0, yOffset, zOffset);
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Desired Position: Target position + Calculated Offset + Manual Adjustments
        // We keep X same as target (plus manual offset)
        // We keep Z relative to world North (Fixed Rotation) or Target?
        // User asked for "GPS Navigator" feel. usually that means Fixed Orientation (North Up) OR Follow Heading.
        // For now, let's do Fixed Orientation (North Up) which feels more like a stable map, 
        // with the option later to rotate around Y if they want "Driving Mode".
        
        Vector3 desiredPosition = target.position + _calculatedOffset + manualOffset;
        
        // Smoothly interpolate
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;

        // Always look at the target (or maintain fixed rotation)
        // Ideally fixed rotation of 75 degrees feels best for strategy
        transform.rotation = Quaternion.Euler(pitchAngle, 0, 0);
    }
}
