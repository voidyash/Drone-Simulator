using DroneSimulator.Environment;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneSimulator.Drones
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FlightController : MonoBehaviour
    {
        [Header("Flight")]
        [Min(1f)] [SerializeField] private float maximumSpeed = 18f;
        [Min(0.1f)] [SerializeField] private float acceleration = 14f;
        [Min(0.1f)] [SerializeField] private float deceleration = 18f;
        [Min(1f)] [SerializeField] private float yawSpeed = 100f;
        [Min(0.01f)] [SerializeField] private float mouseYawSensitivity = 0.12f;

        [Header("Environment Response (FPV-visible)")]
        [Tooltip("Bank/pitch degrees per m/s of local velocity. Makes wind drift visible as FPV tilt.")]
        [Min(0f)] [SerializeField] private float tiltDegreesPerMps = 1.4f;
        [Tooltip("Max visible bank angle in degrees.")]
        [Min(1f)] [SerializeField] private float maxBankDegrees = 22f;
        [Tooltip("How fast the FPV tilt catches up.")]
        [Min(0.1f)] [SerializeField] private float tiltResponsiveness = 6f;
        [Header("Body Bank (visual only)")]
        [Tooltip("Whole-aircraft roll degrees per m/s of lateral velocity. Applied to the visual model child only — flight rotation is untouched.")]
        [Min(0f)] [SerializeField] private float bodyBankGain = 2.5f;
        [Tooltip("Max visible whole-drone bank angle in degrees.")]
        [Min(1f)] [SerializeField] private float maxBodyBankDegrees = 35f;
        [Tooltip("How fast the body bank catches up / returns to neutral.")]
        [Min(0.1f)] [SerializeField] private float bankResponsiveness = 6f;

        private Rigidbody body;
        private WindSystem windSystem;
        private Camera fpvCamera;
        private Transform droneVisual;
        private Quaternion visualBaseRotation = Quaternion.identity;
        private bool visualBaseCaptured;
        private Vector3? autopilotVelocity;
        private bool flightEnabled = true;

        [Header("Acrobatics")]
        [Tooltip("Degrees per second during a flip maneuver.")]
        [Min(90f)] [SerializeField] private float flipRateDegreesPerSecond = 360f;
        [Tooltip("World-Y correction gain (1/s) while a flip holds altitude.")]
        [Min(0f)] [SerializeField] private float altitudeHoldGain = 3f;
        [Tooltip("Max vertical correction speed (m/s) during altitude hold.")]
        [Min(0f)] [SerializeField] private float maxAltitudeCorrection = 6f;

        [Header("Impact Stability")]
        [Tooltip("Angular damping: bleeds off collision-induced spin so the drone recovers instead of rotating forever. Intentional rotation (yaw/flips/bank) is transform-driven and unaffected.")]
        [Min(0f)] [SerializeField] private float angularStabilityDamping = 2.5f;
        [Tooltip("Hard cap on angular velocity (rad/s): prevents violent post-impact spin while preserving ordinary impact reactions.")]
        [Min(1f)] [SerializeField] private float maxAngularVelocity = 10f;

        private bool flipping;
        private Vector3 flipAxis = Vector3.right;
        private float flipRemaining;
        private float holdAltitudeY;
        private InputActionMap droneActionMap;
        private InputAction moveAction;
        private InputAction altitudeAction;
        private InputAction turnAction;
        private InputAction lookAction;

        public float MaximumSpeed => maximumSpeed;
        public Vector3 Velocity => body == null ? Vector3.zero : body.linearVelocity;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // Fast-moving racer vs thin geometry: continuous detection stops
            // high-speed tunneling through the floor, walls and gates.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.angularDamping = angularStabilityDamping;
            ResolveWindSystem();
            ResolveFpvCamera();
            BindFlightControls();
        }

        private void OnEnable()
        {
            // Re-resolve (not just re-enable): after a domain/script reload the
            // cached map can be stale ("Map must be contained in state").
            // BindFlightControls is idempotent.
            BindFlightControls();
        }

        private void Update()
        {
            if (!flightEnabled || turnAction == null || lookAction == null)
            {
                return;
            }

            PollFlipInput();

            // Pilot yaw (no autopilot active).
            if (autopilotVelocity.HasValue)
            {
                return;
            }

            var gamepadOrKeyboardTurn = turnAction.ReadValue<float>() * yawSpeed * Time.deltaTime;
            var mouseTurn = lookAction.ReadValue<Vector2>().x * mouseYawSensitivity;
            float turbulenceYaw = GetTurbulenceYawRate() * Time.deltaTime;
            transform.Rotate(0f, gamepadOrKeyboardTurn + mouseTurn + turbulenceYaw, 0f, Space.Self);
        }

        private void LateUpdate()
        {
            ApplyFpvTilt();
            ApplyBodyBank();
        }

        private void FixedUpdate()
        {
            if (!flightEnabled)
            {
                return;
            }

            if (body == null || moveAction == null || altitudeAction == null)
            {
                return;
            }

            ProgressFlip();

            var movement = moveAction.ReadValue<Vector2>();
            var altitude = altitudeAction.ReadValue<float>();
            var localDirection = new Vector3(movement.x, altitude, movement.y);
            localDirection = Vector3.ClampMagnitude(localDirection, 1f);

            var targetVelocity = transform.TransformDirection(localDirection) * maximumSpeed;
            if (autopilotVelocity.HasValue)
            {
                targetVelocity = autopilotVelocity.Value;
            }

            // Wind drift: added to the chased target so wind visibly carries the
            // drone instead of being overwritten by the velocity-chase next frame.
            Vector3 windDrift = Vector3.zero;
            Vector3 gustDrift = Vector3.zero;
            if (windSystem == null)
            {
                ResolveWindSystem();
            }

            if (windSystem != null)
            {
                windDrift = windSystem.GetWindVector();
                gustDrift = windSystem.GetTurbulenceVelocity(Time.time);
                targetVelocity += windDrift + gustDrift;
            }

            if (flipping)
            {
                // World-space altitude hold: correct toward the Y captured at
                // flip start, independent of body orientation (never uses
                // transform.up/local axes). Pilot vertical input above stays
                // authoritative; this only trims world-Y drift so flips and
                // rolls don't cost altitude. Single authority: the chase
                // below applies the summed target, no extra force system.
                float correction = Mathf.Clamp(
                    (holdAltitudeY - transform.position.y) * altitudeHoldGain,
                    -maxAltitudeCorrection,
                    maxAltitudeCorrection);
                targetVelocity.y += correction;
            }

            var changingSpeed = targetVelocity.sqrMagnitude > body.linearVelocity.sqrMagnitude;
            var rate = changingSpeed ? acceleration : deceleration;
            body.linearVelocity = Vector3.MoveTowards(
                body.linearVelocity,
                targetVelocity,
                rate * Time.fixedDeltaTime);

            // Post-collision spin control: cap runaway angular velocity from
            // impact torque so the drone reacts, then damps back to
            // controllable flight. Intentional rotation (yaw, flips, visual
            // bank) never flows through angularVelocity and is unaffected.
            if (body.angularVelocity.sqrMagnitude > maxAngularVelocity * maxAngularVelocity)
            {
                body.angularVelocity = Vector3.ClampMagnitude(body.angularVelocity, maxAngularVelocity);
            }

            ApplyEnvironmentDisturbance();
        }

        public void SetWindSystem(WindSystem system)
        {
            windSystem = system;
        }

        /// <summary>
        /// External velocity override: when set, this velocity replaces pilot
        /// input as the chase target (wind drift still applies). Null restores
        /// pilot control.
        /// </summary>
        public void SetAutopilotVelocity(Vector3? velocity)
        {
            autopilotVelocity = velocity;
        }

        /// <summary>
        /// Starts a 180-degree flip maneuver with world-space altitude hold.
        /// pitchFlip=true rotates about local X (forward flip); false rotates
        /// about local Z (roll flip). Orientation is never auto-leveled: the
        /// drone stays inverted afterwards until the pilot rotates it back.
        /// X = pitch flip, N = roll flip (Keyboard.current polling, same
        /// precedent as the environment/sensor runtime keys).
        /// </summary>
        public void TriggerFlip(bool pitchFlip)
        {
            if (flipping || !flightEnabled)
            {
                return;
            }

            flipping = true;
            flipAxis = pitchFlip ? Vector3.right : Vector3.forward;
            flipRemaining = 180f;
            holdAltitudeY = transform.position.y;
        }

        public bool IsFlipping => flipping;

        /// <summary>World-Y altitude captured at flip start (test/UI readback).</summary>
        public float HoldAltitudeY => holdAltitudeY;

        private void PollFlipInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || flipping)
            {
                return;
            }

            if (keyboard.xKey.wasPressedThisFrame)
            {
                TriggerFlip(true);
            }
            else if (keyboard.nKey.wasPressedThisFrame)
            {
                TriggerFlip(false);
            }
        }

        private void ProgressFlip()
        {
            if (!flipping || body == null)
            {
                return;
            }

            // Physics-space rotation (not Transform): interpolated/sleeping
            // bodies drop raw Transform writes, which would stall or erase
            // the maneuver. MoveRotation is adopted by the solver and wakes
            // the body, so the flip always completes exactly.
            float step = Mathf.Min(flipRateDegreesPerSecond * Time.fixedDeltaTime, flipRemaining);
            body.MoveRotation(body.rotation * Quaternion.AngleAxis(step, flipAxis));
            flipRemaining -= step;
            if (flipRemaining <= 0f)
            {
                flipping = false;
            }
        }

        /// <summary>
        /// Hull integrity loss (DroneHealth): cuts velocity control and lets
        /// gravity take the drone down. Called once when health reaches 0.
        /// </summary>
        public void DisableFlight()
        {
            flightEnabled = false;
            autopilotVelocity = null;
            if (body != null)
            {
                body.useGravity = true;
            }
        }

        private void ResolveWindSystem()
        {
            if (windSystem == null)
            {
                windSystem = FindAnyObjectByType<WindSystem>();
            }
        }

        private void ResolveFpvCamera()
        {
            if (fpvCamera != null)
            {
                return;
            }

            fpvCamera = GetComponentInChildren<Camera>();
            if (fpvCamera == null && Camera.main != null)
            {
                fpvCamera = Camera.main;
            }
        }

        /// <summary>
        /// Whole-aircraft visual bank: rolls the 3D model child (not the
        /// flight body) into lateral motion. The visual root carries a 180-degree
        /// yaw, which mirrors its local Z axis, so the bank angle is negated to
        /// match the FPV camera's roll direction. Absolute target recomputed
        /// every frame from a stable base rotation: never accumulates, returns
        /// smoothly to neutral when lateral motion stops. Flight rotation,
        /// flips, altitude hold and physics are untouched.
        /// </summary>
        private void ApplyBodyBank()
        {
            if (body == null)
            {
                return;
            }

            if (droneVisual == null)
            {
                droneVisual = transform.Find("PBR Racing Drone");
                visualBaseCaptured = false;
                if (droneVisual == null)
                {
                    return;
                }
            }

            if (!visualBaseCaptured)
            {
                visualBaseRotation = droneVisual.localRotation;
                visualBaseCaptured = true;
            }

            Vector3 localVel = transform.InverseTransformDirection(body.linearVelocity);
            float targetBank = Mathf.Clamp(-localVel.x * bodyBankGain, -maxBodyBankDegrees, maxBodyBankDegrees);
            Quaternion target = visualBaseRotation * Quaternion.Euler(0f, 0f, -targetBank);
            droneVisual.localRotation = Quaternion.Slerp(
                droneVisual.localRotation,
                target,
                Mathf.Clamp01(bankResponsiveness * Time.deltaTime));
        }

        private float GetTurbulenceYawRate()
        {
            if (windSystem == null || windSystem.Turbulence <= 0.01f)
            {
                return 0f;
            }

            float wobble = (Mathf.PerlinNoise(Time.time * 1.9f, 4.4f) - 0.5f) * 2f;
            return wobble * (windSystem.Turbulence / 100f) * 45f;
        }

        /// <summary>
        /// Leans the FPV camera with body velocity so motion is visible as
        /// horizon tilt: lateral velocity rolls, forward/backward velocity
        /// pitches (forward flight tips the view down like a nosing-down
        /// racing quad). Same gain and clamp on both axes. Physics body is
        /// untouched — visuals only.
        /// </summary>
        private void ApplyFpvTilt()
        {
            if (fpvCamera == null || body == null)
            {
                return;
            }

            Vector3 localVel = transform.InverseTransformDirection(body.linearVelocity);
            float targetRoll = Mathf.Clamp(-localVel.x * tiltDegreesPerMps, -maxBankDegrees, maxBankDegrees);
            // Forward flight stays level; only backward flight pitches the
            // view (existing behavior preserved exactly).
            float targetPitch = 0f;
            if (localVel.z < 0f)
            {
                targetPitch = Mathf.Clamp(-localVel.z * tiltDegreesPerMps, -maxBankDegrees, maxBankDegrees);
            }

            if (windSystem != null && windSystem.Turbulence > 0.01f)
            {
                float k = windSystem.Turbulence / 100f;
                targetRoll += (Mathf.PerlinNoise(Time.time * 2.3f, 9.1f) - 0.5f) * 2f * 8f * k;
                targetPitch += (Mathf.PerlinNoise(3.7f, Time.time * 2.1f) - 0.5f) * 2f * 5f * k;
            }

            Quaternion target = Quaternion.Euler(targetPitch, 0f, targetRoll);
            fpvCamera.transform.localRotation = Quaternion.Slerp(
                fpvCamera.transform.localRotation,
                target,
                Mathf.Clamp01(tiltResponsiveness * Time.deltaTime));
        }

        private void ApplyEnvironmentDisturbance()
        {
            if (windSystem == null)
            {
                ResolveWindSystem();
                if (windSystem == null)
                {
                    return;
                }
            }

            // Gust punch only — steady wind already drifts via target velocity above.
            // (Previously wind was double-counted here, launching the hovering
            // drone to 70+ m/s at strength 100.)
            var gustPunch = windSystem.GetTurbulenceAcceleration(Time.time);
            if (gustPunch.sqrMagnitude > 0f)
            {
                body.AddForce(gustPunch, ForceMode.Acceleration);
            }
        }

        public void ConfigureMaximumSpeed(float configuredMaximumSpeed)
        {
            maximumSpeed = Mathf.Max(1f, configuredMaximumSpeed);
        }

        private void BindFlightControls()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("No project-wide InputActionAsset is configured. Flight controls are unavailable.", this);
                return;
            }

            droneActionMap = actions.FindActionMap("Drone", false);
            if (droneActionMap == null)
            {
                Debug.LogError("The configured InputActionAsset does not contain the required Drone action map.", this);
                return;
            }

            moveAction = droneActionMap.FindAction("Move", true);
            altitudeAction = droneActionMap.FindAction("Altitude", true);
            turnAction = droneActionMap.FindAction("Turn", true);
            lookAction = droneActionMap.FindAction("Look", true);
            droneActionMap.Enable();
        }

        // NOTE: the Drone action map is a project-wide singleton shared by all
        // drones. Do NOT disable it here — destroying one follower would cut
        // input for the survivors ("Map must be contained in state" errors).
        private void OnDestroy()
        {
        }
    }
}
