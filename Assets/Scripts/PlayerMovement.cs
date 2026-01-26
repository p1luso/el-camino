using UnityEngine;
using Mapbox.Unity.Map;
using Mapbox.Unity.Location;
using Mapbox.Utils;

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
    private ILocationProvider _locationProvider;
    private bool _isInitialized = false;

    // Optional event for other game systems (like GAS earning)
    public delegate void SpeedUpdated(float speed);
    public event SpeedUpdated OnSpeedUpdated;

    void Start()
    {
        if (_map == null)
        {
            _map = FindFirstObjectByType<AbstractMap>();
        }

        InitializeLocationProvider();
    }

    void InitializeLocationProvider()
    {
        if (LocationProviderFactory.Instance == null) return;
        
        _locationProvider = LocationProviderFactory.Instance.DefaultLocationProvider;
        if (_locationProvider != null)
        {
            _locationProvider.OnLocationUpdated += HandleLocationUpdated;
            _isInitialized = true;
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
        
        // Use provided speed if available and accurate, otherwise we calculate in Update ?? 
        // Actually, Mapbox location often provides speed, but let's trust Unity movement for visual speed.
    }

    void Update()
    {
        if (!_isInitialized)
        {
            InitializeLocationProvider();
            return;
        }

        MoveAndRotate();
    }

    void MoveAndRotate()
    {
        // 1. Move
        Vector3 previousPosition = transform.position;
        transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * _smoothingSpeed);

        // 2. Calculate Speed (Unity units per frame -> approx speed)
        // For game logic, you might want real GPS speed, but for visuals:
        float dist = Vector3.Distance(transform.position, previousPosition);
        _currentSpeed = dist / Time.deltaTime;
        OnSpeedUpdated?.Invoke(_currentSpeed);

        // 3. Rotate
        Vector3 direction = (_targetPosition - transform.position).normalized;
        
        // Only rotate if we have significant movement to avoid jitter
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * _rotationSpeed);
        }
    }
}
