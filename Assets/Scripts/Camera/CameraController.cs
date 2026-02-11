using UnityEngine;
using UnityEngine.EventSystems;

namespace ElCamino.Camera
{
    public class CameraController : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;

        [Header("Settings")]
        public float smoothSpeed = 5f;
        
        [Header("Zoom & Orbit")]
        [Tooltip("Distance from the target")]
        public float distance = 40f;
        public float minDistance = 10f;
        public float maxDistance = 100f;
        public float zoomSpeed = 2f;
        
        [Header("Pitch (Tilt)")]
        [Tooltip("Height angle in degrees (X rotation)")]
        public float pitchAngle = 60f; 
        public float minPitch = 10f;
        public float maxPitch = 85f;
        public float pitchSpeed = 2f;

        [Tooltip("Current rotation around the target (Y rotation)")]
        public float yawAngle = 0f;
        public float rotationSpeed = 2f;
        
        [Header("Offset Adjustment")]
        [Tooltip("Manual offset to center properly if needed")]
        public Vector3 manualOffset = Vector3.zero;

        private Vector3 _calculatedOffset;

        void Start()
        {
            CalculateOffset();
        }

        void OnValidate()   
        {
            CalculateOffset();
        }

        void Update()
        {
            HandleInput();
            CalculateOffset();
        }

        void HandleInput()
        {
            // 1. Handle Touch Input (Android / BlueStacks)
            // NOTE: BlueStacks simulates touch, but sometimes maps mouse to touch unexpectedly.
            // We will prioritize TOUCH if available, but also allow MOUSE dragging as a fallback/hybrid.
            
            bool isTouching = Input.touchCount > 0;

            if (isTouching)
            {
                // Check if touching UI
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
                    return;

                if (Input.touchCount == 1)
                {
                    // Orbit & Pitch with 1 Finger
                    Touch touch = Input.GetTouch(0);
                    if (touch.phase == TouchPhase.Moved)
                    {
                        // Horizontal drag -> Yaw (Orbit)
                        yawAngle += touch.deltaPosition.x * rotationSpeed * 0.2f;

                        // Vertical drag -> Pitch (Tilt)
                        // Swipe Up -> Decrease Pitch (Look up towards horizon)
                        // Swipe Down -> Increase Pitch (Look down towards ground)
                        pitchAngle -= touch.deltaPosition.y * pitchSpeed * 0.1f;
                        pitchAngle = Mathf.Clamp(pitchAngle, minPitch, maxPitch);
                    }
                }
                else if (Input.touchCount == 2)
                {
                    // Pinch to Zoom ONLY (Simpler logic)
                    Touch touch0 = Input.GetTouch(0);
                    Touch touch1 = Input.GetTouch(1);

                    Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
                    Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

                    float prevTouchDeltaMag = (touch0PrevPos - touch1PrevPos).magnitude;
                    float touchDeltaMag = (touch0.position - touch1.position).magnitude;

                    float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

                    distance += deltaMagnitudeDiff * zoomSpeed * 0.05f;
                    distance = Mathf.Clamp(distance, minDistance, maxDistance);
                }
            }
            else
            {
                // Mouse Input (Editor / BlueStacks Fallback)
                if (Input.GetMouseButton(0)) // Left Click Drag
                {
                    // Check UI
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                        return;

                    float mouseX = Input.GetAxis("Mouse X");
                    float mouseY = Input.GetAxis("Mouse Y");

                    yawAngle += mouseX * rotationSpeed * 2f;
                    
                    // Invert Y for intuitive pitch control
                    pitchAngle -= mouseY * pitchSpeed * 2f;
                    pitchAngle = Mathf.Clamp(pitchAngle, minPitch, maxPitch);
                }

                // Scroll to Zoom
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll != 0)
                {
                    distance -= scroll * zoomSpeed * 10f;
                    distance = Mathf.Clamp(distance, minDistance, maxDistance);
                }
            }
        }

        void CalculateOffset()
        {
            // Calculate rotation based on Pitch (X) and Yaw (Y)
            Quaternion rotation = Quaternion.Euler(pitchAngle, yawAngle, 0);

            // Direction vector based on rotation (pointing backwards from target)
            Vector3 direction = rotation * Vector3.back;

            // Final offset
            _calculatedOffset = direction * distance + manualOffset;
        }

        void LateUpdate()
        {
            if (target == null) return;

            // Target position (Smooth follow logic could go here, but for Orbit we usually want instant or very fast follow)
            Vector3 desiredPosition = target.position + _calculatedOffset;
            
            // Dynamic Smooth Speed:
            // If we are rotating (Input active), snap faster to avoid "drifting" arc.
            // If just following player movement, use smooth speed.
            float currentSmooth = (Input.touchCount > 0 || Input.GetMouseButton(0)) ? smoothSpeed * 2f : smoothSpeed;

            transform.position = Vector3.Lerp(transform.position, desiredPosition, currentSmooth * Time.deltaTime);
            
            // Always look at the target
            transform.LookAt(target.position + manualOffset);
        }
    }
}