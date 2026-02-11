namespace Mapbox.Unity.MeshGeneration.Modifiers
{
    using UnityEngine;
    using Mapbox.Unity.MeshGeneration.Data;
    using Mapbox.Unity.MeshGeneration.Components;

    [CreateAssetMenu(menuName = "Mapbox/Modifiers/Road Segment Modifier")]
    public class RoadModifier : GameObjectModifier
    {
        [Header("Materials")]
        [Tooltip("Material inicial de la calle (Normal/Asfalto) que escribe en Stencil.")]
        public Material roadMaskMaterial;
        
        [Tooltip("Material de la calle cuando es visitada (Brillante/Pintada).")]
        public Material visitedMaterial;

        [Header("Settings")]
        public string playerTag = "Player";
        public float detectionMargin = 3f;

        public override void Run(VectorEntity ve, UnityTile tile)
        {
            // Debug para verificar que se está ejecutando
            // Debug.Log($"[RoadModifier] Procesando calle: {ve.GameObject.name}");

            // 1. Asignar material base (Mask)
            if (roadMaskMaterial != null)
            {
                var renderer = ve.GameObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material = roadMaskMaterial;
                    
                    // Fix: Physical lift to completely eliminate Z-fighting with terrain
                    // This is more effective than shader offsets for Mapbox
                    ve.GameObject.transform.localPosition += new Vector3(0, 0.05f, 0);
                    
                    // Optional: Disable shadows to reduce noise
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
            else
            {
                Debug.LogWarning("[RoadModifier] Road Mask Material no asignado. La calle no se pintará.");
            }

            // 2. Agregar lógica de visitado
            var roadSegment = ve.GameObject.AddComponent<RoadSegment>();
            roadSegment.playerTag = playerTag;
            roadSegment.detectionMargin = detectionMargin;
            roadSegment.visitedMaterial = visitedMaterial;
            
            // Assign Mapbox Feature ID for persistence
            if (ve.Feature != null && ve.Feature.Data != null)
            {
                roadSegment.RoadId = ve.Feature.Data.Id;
            }

            // Ensure collider exists for "Snap to Road" logic in TrailRecorder
            if (ve.GameObject.GetComponent<Collider>() == null)
            {
                var mc = ve.GameObject.AddComponent<MeshCollider>();
                // Fix: Player collision issues
                // 1. "Failed extracting collision mesh because vertex... nan": This means the mesh is bad/corrupt.
                //    Mapbox sometimes generates degenerate triangles.
                // 2. "Se sigue chocando contra todo": Player physics collides with road.
                
                // Solution A: Use 'cookingOptions' to clean mesh? (Only in newer Unity versions)
                // Solution B: Use BoxCollider instead? No, roads are complex.
                // Solution C: Disable collider for physics, enable only for Raycast?
                
                // We will move the road to a specific Layer "Roads" (e.g. layer 9)
                // and we rely on the user to uncheck collision between "Player" and "Roads" in Physics Settings.
                // BUT we can't change project settings from script easily.
                
                // Alternative: Set collider as Trigger? NO, Unity forbids Concave Triggers.
                // Alternative: Set collider as Convex? NO, roads are concave.
                
                // BEST FIX for "NAN" error and "Flying":
                // Don't use MeshCollider if the mesh is invalid. But we can't know easily.
                // We will try to sanitize.
                
                // For the "Flying" issue: 
                // We'll set the GameObject layer to "Ignore Raycast" (Layer 2) TEMPORARILY
                // so the Player (if using Raycasts for movement) might ignore it.
                // BUT TrailRecorder needs to raycast it.
                
                // Let's create a dedicated layer logic or tag.
                // We already added 'RoadSegment' component.
                
                // CRITICAL FIX: The "NAN" error crashes the collider creation.
                // If the mesh is bad, we skip collider.
                Mesh mesh = ve.GameObject.GetComponent<MeshFilter>().sharedMesh;
                if (mesh != null)
                {
                    // Simple check for validity
                    if (mesh.vertexCount > 0) 
                    {
                        mc.sharedMesh = mesh; // Explicit assign
                        mc.convex = false;
                        mc.isTrigger = false;
                        
                        // NOTE: Physical collision with Player is handled in RoadSegment.cs (Start method)
                        // via Physics.IgnoreCollision to prevent "flying" or stumbling.
                        mesh.RecalculateBounds();
                    }
                }
            }
        }
    }
}
