// VehicleForceFeedback.cs — Logitech G920 Force Feedback for curbs, bumps, and collisions.
// Monitors suspension variations, vertical acceleration jolts, and collision impulses in FixedUpdate.
// Plays clear, short haptic impulses with strict cooldown and duration control.
using System;
using UnityEngine;

#if !UNITY_WSA || UNITY_EDITOR
using AutoSim.CompanionApp;
#endif

namespace AutoSim.Vehicle
{
    /// <summary>
    /// Delivers realistic haptic impulses to the Logitech G920 steering wheel
    /// when the vehicle crosses road bumps, impacts curbs, or collides with obstacles.
    /// Runs exclusively on the PC host (UWP-safe).
    /// </summary>
    [AddComponentMenu("AutoSim/Vehicle/Vehicle Force Feedback")]
    [RequireComponent(typeof(VehicleController))]
    public class VehicleForceFeedback : MonoBehaviour
    {
        // ──────────────────────────── Serialized Settings ─────────────────────────────

        [Header("General")]
        [Tooltip("Enable haptic force feedback effects.")]
        [SerializeField] private bool enableFFB = true;

        [Tooltip("Master force feedback multiplier (0..1).")]
        [Range(0f, 1f)]
        [SerializeField] private float impactIntensity = 0.8f;

        [Header("Suspension Shock Detection")]
        [Tooltip("Minimum suspension compression velocity (units/sec) required to trigger bump haptics.")]
        [SerializeField] private float suspensionBumpThreshold = 3.5f;

        [Tooltip("Minimum vertical acceleration jolt (m/s^2) required to trigger curb shock.")]
        [SerializeField] private float verticalJoltThreshold = 14f;

        [Header("Collision Impact")]
        [Tooltip("Minimum collision impulse magnitude to trigger collision FFB.")]
        [SerializeField] private float collisionImpulseThreshold = 800f;

        [Header("Timing & Cooldown")]
        [Tooltip("Duration of active FFB pulse in seconds.")]
        [Range(0.04f, 0.25f)]
        [SerializeField] private float impulseDuration = 0.10f;

        [Tooltip("Minimum cooldown between successive impulses to avoid continuous vibration.")]
        [Range(0.08f, 0.40f)]
        [SerializeField] private float cooldownTime = 0.15f;

        // ──────────────────────────── Internal State ──────────────────────────────────

        private VehicleController vehicleController;
        private Rigidbody vehicleRigidbody;
        private WheelCollider[] wheelColliders;
        private float[] lastWheelComp;

        private float lastVerticalVelocity;
        private float cooldownTimer = 0f;
        private float activePulseTimer = 0f;
        private bool isPulseActive = false;

        // ──────────────────────────── Unity Lifecycle ─────────────────────────────────

        private void Awake()
        {
            vehicleController = GetComponent<VehicleController>();
            vehicleRigidbody = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            DiscoverWheels();

#if !UNITY_WSA || UNITY_EDITOR
            // Apply subtle centering spring to maintain authentic wheel weight
            if (enableFFB)
            {
                LogitechSDKWrapper.PlayCenteringSpringSafe(0, 0, 40, 30);
            }
#endif
        }

        private void OnDisable()
        {
#if !UNITY_WSA || UNITY_EDITOR
            LogitechSDKWrapper.StopAllForcesSafe(0);
#endif
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            // Update timers
            if (cooldownTimer > 0f)
                cooldownTimer -= dt;

            if (isPulseActive)
            {
                activePulseTimer -= dt;
                if (activePulseTimer <= 0f)
                {
                    isPulseActive = false;
#if !UNITY_WSA || UNITY_EDITOR
                    LogitechSDKWrapper.StopBumpyRoadSafe(0);
#endif
                }
            }

            if (!enableFFB) return;

            // Monitor wheel suspension compression rate
            CheckSuspensionBumps(dt);

            // Monitor body vertical jolt
            CheckVerticalJolt(dt);
        }

        // ──────────────────────────── Shock & Bump Detection ──────────────────────────

        private void DiscoverWheels()
        {
            wheelColliders = GetComponentsInChildren<WheelCollider>();
            if (wheelColliders != null && wheelColliders.Length > 0)
            {
                lastWheelComp = new float[wheelColliders.Length];
                for (int i = 0; i < wheelColliders.Length; i++)
                {
                    lastWheelComp[i] = GetNormalisedCompression(wheelColliders[i]);
                }
            }
        }

        private float GetNormalisedCompression(WheelCollider wheel)
        {
            if (wheel == null) return 0f;

            WheelHit hit;
            if (wheel.GetGroundHit(out hit))
            {
                float travel = (-wheel.transform.InverseTransformPoint(hit.point).y - wheel.radius)
                               / Mathf.Max(0.01f, wheel.suspensionDistance);
                return Mathf.Clamp01(travel);
            }
            return 0f;
        }

        private void CheckSuspensionBumps(float dt)
        {
            if (wheelColliders == null || wheelColliders.Length == 0)
            {
                DiscoverWheels();
                return;
            }

            float maxVelocity = 0f;

            for (int i = 0; i < wheelColliders.Length; i++)
            {
                var wheel = wheelColliders[i];
                if (wheel == null) continue;

                float currentComp = GetNormalisedCompression(wheel);
                float compVelocity = Mathf.Abs(currentComp - lastWheelComp[i]) / dt;
                lastWheelComp[i] = currentComp;

                if (wheel.isGrounded && compVelocity > maxVelocity)
                {
                    maxVelocity = compVelocity;
                }
            }

            if (maxVelocity > suspensionBumpThreshold && cooldownTimer <= 0f)
            {
                float excess = maxVelocity - suspensionBumpThreshold;
                float shockFraction = Mathf.Clamp01(excess / 6.0f);
                int magnitude = Mathf.RoundToInt(Mathf.Lerp(30f, 100f, shockFraction) * impactIntensity);

                TriggerHapticPulse(magnitude);
            }
        }

        private void CheckVerticalJolt(float dt)
        {
            if (vehicleRigidbody == null) return;

            float currentVertVel = vehicleRigidbody.velocity.y;
            float vertAccel = Mathf.Abs(currentVertVel - lastVerticalVelocity) / dt;
            lastVerticalVelocity = currentVertVel;

            if (vertAccel > verticalJoltThreshold && cooldownTimer <= 0f)
            {
                float excess = vertAccel - verticalJoltThreshold;
                float shockFraction = Mathf.Clamp01(excess / 20.0f);
                int magnitude = Mathf.RoundToInt(Mathf.Lerp(35f, 100f, shockFraction) * impactIntensity);

                TriggerHapticPulse(magnitude);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!enableFFB || cooldownTimer > 0f) return;

            float impulseMag = collision.impulse.magnitude;
            if (impulseMag > collisionImpulseThreshold)
            {
                float excess = impulseMag - collisionImpulseThreshold;
                float shockFraction = Mathf.Clamp01(excess / 4000f);
                int magnitude = Mathf.RoundToInt(Mathf.Lerp(45f, 100f, shockFraction) * impactIntensity);

                TriggerHapticPulse(magnitude);
            }
        }

        // ──────────────────────────── Haptic Pulse Execution ──────────────────────────

        /// <summary>
        /// Fires a short, distinct FFB impulse to the steering wheel with strict duration and cooldown.
        /// </summary>
        /// <param name="magnitudePercent">Force magnitude in range 1..100%.</param>
        public void TriggerHapticPulse(int magnitudePercent)
        {
            magnitudePercent = Mathf.Clamp(magnitudePercent, 10, 100);

#if !UNITY_WSA || UNITY_EDITOR
            LogitechSDKWrapper.PlayImpactForceSafe(0, magnitudePercent);
#endif

            isPulseActive = true;
            activePulseTimer = impulseDuration;
            cooldownTimer = cooldownTime;
        }
    }
}
