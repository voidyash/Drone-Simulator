using System;
using DroneSimulator.Configuration;
using UnityEngine;

namespace DroneSimulator.Simulation
{
    public sealed class SimulationClock : MonoBehaviour
    {
        private static readonly float[] Multipliers = { 0.25f, 0.5f, 1f, 2f, 4f };

        private float baseFixedDeltaTime;
        private int multiplierIndex = 2;
        private bool initialized;

        public event Action<float> MultiplierChanged;

        public float TimeMultiplier => Multipliers[multiplierIndex];
        public int MultiplierIndex => multiplierIndex;

        public void Initialize(SimulationSpeedPreset preset)
        {
            if (!initialized)
            {
                baseFixedDeltaTime = Time.fixedDeltaTime;
                initialized = true;
            }

            SetMultiplierIndex((int)preset);
        }

        public void IncreaseMultiplier()
        {
            SetMultiplierIndex(multiplierIndex + 1);
        }

        public void DecreaseMultiplier()
        {
            SetMultiplierIndex(multiplierIndex - 1);
        }

        public void SetMultiplier(float requestedMultiplier)
        {
            var closestIndex = 0;
            var closestDistance = Mathf.Abs(Multipliers[0] - requestedMultiplier);

            for (var index = 1; index < Multipliers.Length; index++)
            {
                var distance = Mathf.Abs(Multipliers[index] - requestedMultiplier);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestIndex = index;
                }
            }

            SetMultiplierIndex(closestIndex);
        }

        private void SetMultiplierIndex(int requestedIndex)
        {
            multiplierIndex = Mathf.Clamp(requestedIndex, 0, Multipliers.Length - 1);
            var multiplier = Multipliers[multiplierIndex];
            Time.timeScale = multiplier;
            Time.fixedDeltaTime = baseFixedDeltaTime * multiplier;
            MultiplierChanged?.Invoke(multiplier);
        }

        private void OnDestroy()
        {
            if (!initialized)
            {
                return;
            }

            Time.timeScale = 1f;
            Time.fixedDeltaTime = baseFixedDeltaTime;
        }
    }
}
