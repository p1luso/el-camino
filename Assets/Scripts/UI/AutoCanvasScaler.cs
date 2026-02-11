using UnityEngine;
using UnityEngine.UI;

namespace ElCamino.UI
{
    [RequireComponent(typeof(Canvas))]
    public class AutoCanvasScaler : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnRuntimeMethodLoad()
        {
            // Find all Canvases and apply fix
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            foreach (var canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    if (canvas.GetComponent<AutoCanvasScaler>() == null)
                    {
                        canvas.gameObject.AddComponent<AutoCanvasScaler>();
                    }
                }
            }
        }

        void Awake()
        {
            SetupScaler();
        }

        void SetupScaler()
        {
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            
            // Detect orientation (Portrait vs Landscape)
            // Check actual screen dimensions
            if (Screen.height > Screen.width)
            {
                // Portrait
                scaler.referenceResolution = new Vector2(720, 1280);
            }
            else
            {
                // Landscape
                scaler.referenceResolution = new Vector2(1280, 720);
            }

            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // Expand ensures UI elements are not cut off
        }
    }
}
