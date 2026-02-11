using UnityEngine;
using UnityEngine.UI;
using Mapbox.Unity.Location;

namespace ElCamino.DebugUtils
{
    public class GPSDebugInfo : MonoBehaviour
    {
        void OnGUI()
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 40;
            style.normal.textColor = Color.red;

            GUILayout.BeginArea(new Rect(50, 50, Screen.width - 100, Screen.height - 100));
            
            GUILayout.Label("--- GPS DEBUG INFO ---", style);

            if (!Input.location.isEnabledByUser)
            {
                GUILayout.Label("ERROR: Location service disabled by user", style);
            }
            else
            {
                GUILayout.Label($"Status: {Input.location.status}", style);
            }

            var provider = LocationProviderFactory.Instance.DefaultLocationProvider;
            if (provider != null)
            {
                GUILayout.Label($"Provider: {provider.GetType().Name}", style);
                GUILayout.Label($"Lat/Lon: {provider.CurrentLocation.LatitudeLongitude}", style);
                GUILayout.Label($"IsLocationServiceEnabled: {provider.CurrentLocation.IsLocationServiceEnabled}", style);
            }
            else
            {
                GUILayout.Label("Provider is NULL", style);
            }

            GUILayout.EndArea();
        }
    }
}