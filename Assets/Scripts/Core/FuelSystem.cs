using UnityEngine;
using ElCamino.AI;

namespace ElCamino.Core
{
    public class FuelSystem : MonoBehaviour
    {
        public static FuelSystem Instance { get; private set; }

        [Header("Fuel Settings")]
        public float maxFuel = 100f;
        public float currentFuel = 100f;
        public float consumptionRate = 0.5f; // Units per second
        public float lowFuelThreshold = 20f;

        private bool _hasWarnedLowFuel = false;
        private AICompanionController _aiCompanion;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // Try to find companion if not attached to same object
            if (_aiCompanion == null)
                _aiCompanion = FindObjectOfType<AICompanionController>();
        }

        private void Update()
        {
            if (currentFuel > 0)
            {
                // Consume fuel
                // In a real car game, this would depend on speed/RPM
                currentFuel -= consumptionRate * Time.deltaTime;

                // Clamp
                if (currentFuel < 0) currentFuel = 0;

                // Check Low Fuel
                if (currentFuel <= lowFuelThreshold && !_hasWarnedLowFuel)
                {
                    _hasWarnedLowFuel = true;
                    if (_aiCompanion != null)
                    {
                        _aiCompanion.OnLowFuelWarning();
                    }
                }
                // Reset warning if refueled
                else if (currentFuel > lowFuelThreshold && _hasWarnedLowFuel)
                {
                    _hasWarnedLowFuel = false;
                }
            }
        }

        public void Refuel(float amount)
        {
            currentFuel += amount;
            if (currentFuel > maxFuel) currentFuel = maxFuel;
        }
    }
}