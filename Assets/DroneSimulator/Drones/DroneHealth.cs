using UnityEngine;

namespace DroneSimulator.Drones
{
    /// <summary>
    /// Drone integrity: Health 0-100 (spec DRONES). Damage comes from kinetic
    /// impacts — collisions with hostile drones, terrain and obstacles — not
    /// weapons (none exist in this project). Hostile drones attack by
    /// ramming; closing speed determines damage. Exposed to telemetry/UI.
    /// Impact curve: threshold 12 m/s, 0.3 hull per m/s above it, 2 s grace.
    /// </summary>
    public sealed class DroneHealth : MonoBehaviour
    {
        [SerializeField] private float health = 100f;
        [Tooltip("Closing speed (m/s) below which impacts do no damage.")]
        [Min(0f)] [SerializeField] private float damageThresholdSpeed = 12f;
        [Tooltip("Health lost per m/s of closing speed above the threshold.")]
        [Min(0f)] [SerializeField] private float damagePerMps = 0.3f;
        [Tooltip("Seconds of immunity after a hit so a single pass can't stack damage frame-by-frame.")]
        [Min(0f)] [SerializeField] private float damageGracePeriod = 2f;

        private DroneController droneController;
        private float lastDamageTime = float.NegativeInfinity;

        public float Health => health;
        public float NormalizedHealth => Mathf.Clamp01(health / 100f);
        public bool IsOperational => health > 0f;

        public event System.Action<float> HealthChanged;

        private void Awake()
        {
            droneController = GetComponent<DroneController>();
        }

        public void SetHealth(float value)
        {
            float previous = health;
            health = Mathf.Clamp(value, 0f, 100f);
            if (!Mathf.Approximately(previous, health))
            {
                HealthChanged?.Invoke(health);
            }

            // Hull gone → flight control cut, gravity takes over (DroneFall).
            if (previous > 0f && health <= 0f && droneController != null
                && droneController.FlightController != null)
            {
                droneController.FlightController.DisableFlight();
            }
        }

        public void Repair()
        {
            SetHealth(100f);
        }

        /// <summary>Runtime tuning of the impact curve.</summary>
        public void Configure(float thresholdSpeed, float damagePerMpsValue, float gracePeriod)
        {
            damageThresholdSpeed = Mathf.Max(0f, thresholdSpeed);
            damagePerMps = Mathf.Max(0f, damagePerMpsValue);
            damageGracePeriod = Mathf.Max(0f, gracePeriod);
        }

        /// <summary>
        /// Applies impact damage from a collision. Damage scales with closing
        /// speed above the threshold, so slow contact is harmless and head-on
        /// rams at speed are severe.
        /// </summary>
        public void ApplyImpact(float closingSpeed)
        {
            if (closingSpeed <= damageThresholdSpeed || health <= 0f)
            {
                return;
            }

            if (Time.time - lastDamageTime < damageGracePeriod)
            {
                return;
            }

            lastDamageTime = Time.time;
            SetHealth(health - (closingSpeed - damageThresholdSpeed) * damagePerMps);
        }

        public string StateLabel
        {
            get
            {
                if (health <= 0f) return "DESTROYED";
                if (health <= 25f) return "CRITICAL";
                if (health <= 60f) return "DAMAGED";
                return "NOMINAL";
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision == null)
            {
                return;
            }

            // Kinetic impact only: closing speed of the contact drives damage,
            // so hostile rams hurt proportionally and gentle bumps don't.
            ApplyImpact(collision.relativeVelocity.magnitude);
        }
    }
}
