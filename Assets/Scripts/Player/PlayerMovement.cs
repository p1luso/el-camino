using UnityEngine;
using Mapbox.Unity.Map;
using Mapbox.Unity.Location;
using Mapbox.Utils;
using ElCamino.Player; // Added namespace

public class PlayerMovement : MonoBehaviour
{
    [SerializeField]
    private AbstractMap _map;

    [Header("Movement Settings")]
    [SerializeField]
    private float _smoothingSpeed = 5f;
    [SerializeField]
    private float _rotationSpeed = 10f;

    [Header("Debug")]
    [SerializeField]
    private float _currentSpeed;

    private Vector3 _targetPosition;
    // We keep a "Virtual" position that represents the raw GPS interpolated position,
    // independent of snapping/avoidance modification.
    private Vector3 _virtualPosition;
    
    private ILocationProvider _locationProvider;
    private bool _hasReceivedFirstLocation = false;

    private PlayerRoadSnapper _snapper;
    private PlayerBuildingAvoidance _avoider;

    // Optional event for other game systems (like GAS earning)
    public delegate void SpeedUpdated(float speed);
    public event SpeedUpdated OnSpeedUpdated;

    void Start()
    {
        if (_map == null)
        {
            _map = FindFirstObjectByType<AbstractMap>();
        }

        _snapper = GetComponent<PlayerRoadSnapper>();
        _avoider = GetComponent<PlayerBuildingAvoidance>();
        _virtualPosition = transform.position;

        InitializeLocationProvider();
    }

    void InitializeLocationProvider()
    {
        if (LocationProviderFactory.Instance == null) return;
        
        _locationProvider = LocationProviderFactory.Instance.DefaultLocationProvider;
        if (_locationProvider != null)
        {
            _locationProvider.OnLocationUpdated += HandleLocationUpdated;
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
        if (_map == null) return;

        // Convert Geo Coordinate to Unity World Position
        _targetPosition = _map.GeoToWorldPosition(location.LatitudeLongitude);
        
        // If this is the very first update, snap to position immediately to avoid "flying" from 0,0,0
        if (!_hasReceivedFirstLocation)
        {
            _virtualPosition = _targetPosition;
            transform.position = _targetPosition;
            _hasReceivedFirstLocation = true;
        }
    }

    void Update()
    {
        if (_locationProvider == null)
        {
            InitializeLocationProvider();
            return;
        }

        MoveAndRotate();
    }

    void MoveAndRotate()
    {
        // 1. Move Virtual Position (Raw GPS Smooth)
        // We use _virtualPosition as the start point, not transform.position, to avoid fighting with Snap/Avoid
        _virtualPosition = Vector3.Lerp(_virtualPosition, _targetPosition, Time.deltaTime * _smoothingSpeed);

        Vector3 finalPos = _virtualPosition;

        // 2. Snap to Road
        bool snapped = false;
        if (_snapper != null && _snapper.enabled)
        {
            finalPos = _snapper.GetSnappedPosition(finalPos, out snapped);
        }

        // 3. Avoid Buildings
        // If we strictly snapped to a road, we DO NOT want to run avoidance,
        // because avoidance might push us off the road into a "safe" zone that is actually a sidewalk/alley.
        // We only run avoidance if we failed to snap (e.g. strict mode off, or first frame).
        if (!snapped && _avoider != null && _avoider.enabled)
        {
            Vector3 avoidanceOffset = _avoider.GetAvoidanceOffset(finalPos);
            finalPos += avoidanceOffset;
        }

        // 4. Apply Final Position
        Vector3 previousPosition = transform.position;
        transform.position = finalPos;
        
        // --- FIX: Prevent Sinking ---
        // Force minimum height to avoid clipping through map
        if (transform.position.y < 1.0f)
        {
             transform.position = new Vector3(transform.position.x, 1.0f, transform.position.z);
        }

        // 5. Calculate Speed (Unity units per frame -> approx speed)
        float dist = Vector3.Distance(transform.position, previousPosition);
        _currentSpeed = dist / Time.deltaTime;
        OnSpeedUpdated?.Invoke(_currentSpeed);

        // 6. Rotate
        // Look towards the target GPS position, but ignore small jitters
        Vector3 diff = _targetPosition - transform.position;
        diff.y = 0; // Flatten direction to prevent car from tilting down

        // Only rotate if we have significant movement to avoid jitter
        if (diff.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(diff);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * _rotationSpeed);
        }
    }
}
