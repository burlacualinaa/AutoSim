// VehicleForceFeedback.cs — Logitech G920 Force Feedback for curbs, bumps, and collisions.
// Monitors suspension variations, vertical acceleration jolts, and collision impulses in FixedUpdate.
// Plays clear, short haptic impulses with strict cooldown and duration control.
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AutoSim.Vehicle
{
    /// <summary>
    /// Delivers realistic haptic impulses to the Logitech G920 steering wheel
    /// when the vehicle crosses road bumps, impacts curbs, or collides with obstacles.
    /// Runs on Windows PC; safely stubs on UWP/HoloLens.
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

        [Header("Centering Spring Force")]
        [Tooltip("Centering spring saturation (0..100%). Lower = softer resistance.")]
        [Range(0, 100)]
        [SerializeField] private int centeringSpringSaturation = 18;

        [Tooltip("Centering spring stiffness coefficient (0..100%). Lower = lighter, smoother wheel.")]
        [Range(0, 100)]
        [SerializeField] private int centeringSpringCoefficient = 14;

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

            // Apply comfortable, lighter centering spring on PC
            if (enableFFB)
            {
                LogitechFFBNative.PlayCenteringSpring(0, 0, centeringSpringSaturation, centeringSpringCoefficient);
            }
        }

        private void OnDisable()
        {
            LogitechFFBNative.StopAllForces(0);
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
                    LogitechFFBNative.StopBumpyRoad(0);
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

            LogitechFFBNative.PlayImpactForce(0, magnitudePercent);

            isPulseActive = true;
            activePulseTimer = impulseDuration;
            cooldownTimer = cooldownTime;
        }

        // ──────────────────────────── Direct P/Invoke FFB Safe Layer ──────────────────

        /// <summary>
        /// Safe P/Invoke wrapper for Logitech Steering Wheel SDK Force Feedback.
        /// Fully self-contained to eliminate asmdef dependency errors.
        /// </summary>
        private static class LogitechFFBNative
        {
            private const string DLL = "LogitechSteeringWheelEnginesWrapper";

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.U1)]
            private static extern bool LogiIsConnected(int index);

            [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.U1)]
            private static extern bool LogiPlayFrontalCollisionForce(int index, int magnitudePercentage);

            [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.U1)]
            private static extern bool LogiPlayBumpyRoadEffect(int index, int magnitudePercentage);

            [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.U1)]
            private static extern bool LogiStopBumpyRoadEffect(int index);

            [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.U1)]
            private static extern bool LogiPlaySpringForce(int index, int offset, int saturation, int coefficient);

            [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.U1)]
            private static extern bool LogiStopSpringForce(int index);

            [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.U1)]
            private static extern bool LogiStopConstantForce(int index);

            private static bool? _isDllPresent = null;

            public static bool IsDllAvailable
            {
                get
                {
                    if (_isDllPresent.HasValue) return _isDllPresent.Value;
                    try
                    {
                        _isDllPresent = LogiIsConnected(0);
                        return _isDllPresent.Value;
                    }
                    catch
                    {
                        _isDllPresent = false;
                        return false;
                    }
                }
            }

            public static bool PlayImpactForce(int index, int magnitudePercentage)
            {
                if (!IsDllAvailable) return false;
                try { return LogiPlayFrontalCollisionForce(index, Mathf.Clamp(magnitudePercentage, 0, 100)); }
                catch { return false; }
            }

            public static bool StopBumpyRoad(int index)
            {
                if (!IsDllAvailable) return false;
                try { return LogiStopBumpyRoadEffect(index); }
                catch { return false; }
            }

            public static bool PlayCenteringSpring(int index, int offset, int saturation, int coefficient)
            {
                if (!IsDllAvailable) return false;
                try { return LogiPlaySpringForce(index, offset, saturation, coefficient); }
                catch { return false; }
            }

            public static void StopAllForces(int index)
            {
                if (!IsDllAvailable) return;
                try
                {
                    LogiStopBumpyRoadEffect(index);
                    LogiStopSpringForce(index);
                    LogiStopConstantForce(index);
                }
                catch { }
            }
#else
            // Safe stubs for UWP / HoloLens 2 ARM64
            public static bool IsDllAvailable => false;
            public static bool PlayImpactForce(int index, int magnitudePercentage) => false;
            public static bool StopBumpyRoad(int index) => false;
            public static bool PlayCenteringSpring(int index, int offset, int saturation, int coefficient) => false;
            public static void StopAllForces(int index) { }
#endif
        }
    }
}
