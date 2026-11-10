// VehicleController.cs — Core vehicle physics controller using Unity WheelColliders.
// Reads input from any IInputProvider and drives a rear-wheel-drive car.
using UnityEngine;
using AutoSim.Input;

namespace AutoSim.Vehicle
{
    /// <summary>
    /// Modular vehicle controller that consumes <see cref="IInputProvider"/> data
    /// and drives a car via Unity's WheelCollider system. Rear-wheel drive with
    /// anti-roll bars and basic traction control.
    /// </summary>
    [AddComponentMenu("AutoSim/Vehicle/Vehicle Controller")]
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Serialized References
        // ---------------------------------------------------------------

        [Header("Wheel Colliders")]
        [Tooltip("Front-left WheelCollider.")]
        [SerializeField] private WheelCollider frontLeft;

        [Tooltip("Front-right WheelCollider.")]
        [SerializeField] private WheelCollider frontRight;

        [Tooltip("Rear-left WheelCollider.")]
        [SerializeField] private WheelCollider rearLeft;

        [Tooltip("Rear-right WheelCollider.")]
        [SerializeField] private WheelCollider rearRight;

        [Header("Physics")]
        [Tooltip("Vehicle Rigidbody (auto-assigned from this GameObject if left empty).")]
        [SerializeField] private Rigidbody vehicleRigidbody;

        [Tooltip("Optional transform used as centre-of-mass override offset.")]
        [SerializeField] private Transform centerOfMassOverride;

        [Header("Anti-Roll Bar")]
        [Tooltip("Anti-roll bar stiffness applied per axle (N). Higher values reduce body roll.")]
        [SerializeField] private float antiRollForce = 5000f;

        [Header("Traction Control")]
        [Tooltip("Maximum allowed longitudinal wheel slip before torque is reduced.")]
        [SerializeField] private float slipThreshold = 0.3f;

        [Tooltip("How aggressively traction control reduces torque (0 = off, 1 = full cut).")]
        [SerializeField, Range(0f, 1f)] private float tractionControlStrength = 0.5f;

        [Header("Input Provider")]
        [Tooltip("Explicit input provider. If unassigned, automatically looks for LogitechG920InputProvider first, then any IInputProvider.")]
        [SerializeField] private MonoBehaviour inputProviderSource;

        /// <summary>Allows procedural assignment of the input provider source.</summary>
        public MonoBehaviour InputProviderSource
        {
            get => inputProviderSource;
            set => inputProviderSource = value;
        }

        // ---------------------------------------------------------------
        //  Public Read-Only Properties
        // ---------------------------------------------------------------

        /// <summary>Current vehicle speed in kilometres per hour.</summary>
        public float SpeedKmh { get; private set; }

        /// <summary>Estimated engine RPM derived from rear wheel average RPM.</summary>
        public float Rpm { get; private set; }

        /// <summary>Current steering angle applied to the front wheels (degrees).</summary>
        public float CurrentSteeringAngle { get; private set; }

        /// <summary>True when the vehicle speed exceeds 0.5 km/h.</summary>
        public bool IsMoving => SpeedKmh > 0.5f;

        // ---------------------------------------------------------------
        //  Input Provider
        // ---------------------------------------------------------------

        /// <summary>
        /// The active input provider feeding steering/throttle/brake data.
        /// Can be swapped at runtime (e.g. keyboard → network).
        /// </summary>
        public IInputProvider ActiveInputProvider { get; set; }

        // ---------------------------------------------------------------
        //  Internal State
        // ---------------------------------------------------------------

        // Cached wheel array for convenience in loops
        private WheelCollider[] allWheels;

        // ---------------------------------------------------------------
        //  Unity Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            // Auto-find Rigidbody if not assigned
            if (vehicleRigidbody == null)
                vehicleRigidbody = GetComponent<Rigidbody>();

            // Configure Rigidbody
            vehicleRigidbody.mass = AutoSimConstants.VEHICLE_MASS;
            vehicleRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            vehicleRigidbody.isKinematic = false;

            // Lower the centre of mass for stability
            if (centerOfMassOverride != null)
            {
                vehicleRigidbody.centerOfMass = transform.InverseTransformPoint(centerOfMassOverride.position);
            }
            else
            {
                vehicleRigidbody.centerOfMass += new Vector3(0f, -0.5f, 0f);
            }

            // Ensure wheel colliders are connected
            EnsureWheelColliders();
        }

        /// <summary>
        /// Automatically discovers and assigns missing WheelColliders from children.
        /// </summary>
        public void EnsureWheelColliders()
        {
            if (frontLeft == null || frontRight == null || rearLeft == null || rearRight == null)
            {
                var colliders = GetComponentsInChildren<WheelCollider>();
                foreach (var wc in colliders)
                {
                    string n = wc.gameObject.name.ToLowerInvariant();
                    Vector3 localPos = transform.InverseTransformPoint(wc.transform.position);

                    // Name-based or position-based detection
                    bool isFront = n.Contains("fl") || n.Contains("fr") || n.Contains("front") || localPos.z > 0.1f;
                    bool isLeft = n.Contains("fl") || n.Contains("rl") || n.Contains("left") || localPos.x < 0f;

                    if (isFront && isLeft && frontLeft == null) frontLeft = wc;
                    else if (isFront && !isLeft && frontRight == null) frontRight = wc;
                    else if (!isFront && isLeft && rearLeft == null) rearLeft = wc;
                    else if (!isFront && !isLeft && rearRight == null) rearRight = wc;
                }
            }

            allWheels = new WheelCollider[] { frontLeft, frontRight, rearLeft, rearRight };
        }

        private void Start()
        {
            // Auto-detect input provider if not explicitly assigned
            if (ActiveInputProvider == null)
            {
#if UNITY_WSA && !UNITY_EDITOR
                // On HoloLens 2 UWP, input is forwarded via Wi-Fi from the PC companion app
                var netProvider = GetComponent<NetworkInputProvider>();
                if (netProvider == null)
                    netProvider = FindObjectOfType<NetworkInputProvider>();

                if (netProvider == null)
                    netProvider = gameObject.AddComponent<NetworkInputProvider>();

                ActiveInputProvider = netProvider;
#else
                if (inputProviderSource is IInputProvider explicitProvider)
                {
                    ActiveInputProvider = explicitProvider;
                }
                else
                {
                    // Prioritize Logitech G920 hardware provider if available on this vehicle
                    var g920 = GetComponent<LogitechG920InputProvider>();
                    if (g920 != null)
                    {
                        ActiveInputProvider = g920;
                    }
                    else
                    {
                        var provider = GetComponent<IInputProvider>();
                        if (provider == null)
                        {
                            var monoBehaviours = FindObjectsOfType<MonoBehaviour>();
                            foreach (var mb in monoBehaviours)
                            {
                                if (mb is IInputProvider p)
                                {
                                    provider = p;
                                    break;
                                }
                            }
                        }
                        ActiveInputProvider = provider;
                    }
                }
#endif

                Debug.Log($"[VehicleController] Active Input Provider bound to: {(ActiveInputProvider != null ? ActiveInputProvider.ProviderName : "NULL")}");
            }
        }

        /// <summary>Allows procedural / editor configuration of wheel colliders.</summary>
        public void SetWheelColliders(WheelCollider fl, WheelCollider fr, WheelCollider rl, WheelCollider rr)
        {
            frontLeft = fl;
            frontRight = fr;
            rearLeft = rl;
            rearRight = rr;
            allWheels = new WheelCollider[] { frontLeft, frontRight, rearLeft, rearRight };
        }

        private void FixedUpdate()
        {
            if (frontLeft == null || frontRight == null || rearLeft == null || rearRight == null)
            {
                EnsureWheelColliders();
                if (frontLeft == null || frontRight == null || rearLeft == null || rearRight == null)
                    return;
            }

            // Calculate speed every physics step regardless of input
            SpeedKmh = vehicleRigidbody.velocity.magnitude * 3.6f;

            // Read input (defaults to zero if no provider)
            float steerInput = 0f;
            float throttleInput = 0f;
            float brakeInput = 0f;

            if (ActiveInputProvider != null && ActiveInputProvider.IsConnected)
            {
                steerInput = ActiveInputProvider.Steering;
                throttleInput = ActiveInputProvider.Throttle;
                brakeInput = ActiveInputProvider.Brake;
            }

            // Anti-stall / Dragging brake safeguard:
            // When accelerating, never let an accidental, uncalibrated or dragging brake stall the car.
            if (throttleInput > 0.05f)
            {
                if (brakeInput < 0.75f || brakeInput <= throttleInput)
                {
                    brakeInput = 0f;
                }
                vehicleRigidbody.WakeUp();
            }

            ApplySteering(steerInput);
            ApplyMotorTorque(throttleInput);
            ApplyBrakes(brakeInput);
            ApplyAntiRollBar(frontLeft, frontRight);
            ApplyAntiRollBar(rearLeft, rearRight);
            UpdateRpm();
        }

        // ---------------------------------------------------------------
        //  Steering
        // ---------------------------------------------------------------

        private void ApplySteering(float input)
        {
            CurrentSteeringAngle = AutoSimConstants.MAX_STEERING_ANGLE * input;
            frontLeft.steerAngle = CurrentSteeringAngle;
            frontRight.steerAngle = CurrentSteeringAngle;
        }

        // ---------------------------------------------------------------
        //  Motor Torque (Rear-Wheel Drive) with Traction Control
        // ---------------------------------------------------------------

        private void ApplyMotorTorque(float throttle)
        {
            float baseTorque = AutoSimConstants.MAX_MOTOR_TORQUE * throttle;

            // Traction control: reduce torque when wheel slip is excessive
            float tcFactorRL = GetTractionControlFactor(rearLeft);
            float tcFactorRR = GetTractionControlFactor(rearRight);

            rearLeft.motorTorque = baseTorque * tcFactorRL;
            rearRight.motorTorque = baseTorque * tcFactorRR;

            // Ensure front wheels are not driven
            frontLeft.motorTorque = 0f;
            frontRight.motorTorque = 0f;
        }

        /// <summary>
        /// Returns a multiplier (0..1) that reduces torque when longitudinal slip
        /// exceeds the threshold. 1 = full torque, 0 = torque fully cut.
        /// </summary>
        private float GetTractionControlFactor(WheelCollider wheel)
        {
            if (tractionControlStrength <= 0f)
                return 1f;

            WheelHit hit;
            if (!wheel.GetGroundHit(out hit))
                return 1f; // Wheel in air — no TC correction

            float slip = Mathf.Abs(hit.forwardSlip);
            if (slip <= slipThreshold)
                return 1f;

            // Linear reduction based on excess slip
            float excessSlip = slip - slipThreshold;
            float reduction = Mathf.Clamp01(excessSlip * tractionControlStrength * 5f);
            return 1f - reduction;
        }

        // ---------------------------------------------------------------
        //  Brakes
        // ---------------------------------------------------------------

        private void ApplyBrakes(float brakeInput)
        {
            float brakeTorque = AutoSimConstants.MAX_BRAKE_TORQUE * brakeInput;

            for (int i = 0; i < allWheels.Length; i++)
            {
                allWheels[i].brakeTorque = brakeTorque;
            }
        }

        // ---------------------------------------------------------------
        //  Anti-Roll Bar
        // ---------------------------------------------------------------

        /// <summary>
        /// Applies an anti-roll bar force between a left/right wheel pair.
        /// Reduces body roll by transferring load to the less-compressed side.
        /// </summary>
        private void ApplyAntiRollBar(WheelCollider leftWheel, WheelCollider rightWheel)
        {
            float travelL = GetSuspensionTravel(leftWheel);
            float travelR = GetSuspensionTravel(rightWheel);

            // Positive value means left is more compressed than right
            float antiRollForceMagnitude = (travelL - travelR) * antiRollForce;

            // Apply counterforce: push down on the less-compressed side
            if (leftWheel.isGrounded)
            {
                vehicleRigidbody.AddForceAtPosition(
                    leftWheel.transform.up * -antiRollForceMagnitude,
                    leftWheel.transform.position);
            }

            if (rightWheel.isGrounded)
            {
                vehicleRigidbody.AddForceAtPosition(
                    rightWheel.transform.up * antiRollForceMagnitude,
                    rightWheel.transform.position);
            }
        }

        /// <summary>
        /// Returns normalised suspension travel (0 = fully extended, 1 = fully compressed).
        /// </summary>
        private float GetSuspensionTravel(WheelCollider wheel)
        {
            WheelHit hit;
            bool grounded = wheel.GetGroundHit(out hit);

            if (grounded)
            {
                // Normalised compression: 0 = rest length, 1 = bottomed out
                return (-wheel.transform.InverseTransformPoint(hit.point).y - wheel.radius)
                       / wheel.suspensionDistance;
            }

            // Fully extended (wheel in air)
            return 0f;
        }

        // ---------------------------------------------------------------
        //  RPM Estimation
        // ---------------------------------------------------------------

        /// <summary>
        /// Estimates engine RPM from the average of the two rear wheel RPMs
        /// with a simple fixed gear-ratio multiplier.
        /// </summary>
        private void UpdateRpm()
        {
            float avgWheelRpm = (Mathf.Abs(rearLeft.rpm) + Mathf.Abs(rearRight.rpm)) * 0.5f;

            // Simple approximation: engine RPM = wheel RPM * final drive * gear ratio
            // Using a fixed combined ratio of ~4.0 for a mid-range estimate
            const float combinedGearRatio = 4.0f;
            Rpm = Mathf.Clamp(avgWheelRpm * combinedGearRatio, 800f, 8000f);
        }

        // ---------------------------------------------------------------
        //  Public Helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Returns the RPM of an individual wheel by index.
        /// Index order: 0=FL, 1=FR, 2=RL, 3=RR.
        /// </summary>
        /// <param name="index">Wheel index (0-3).</param>
        /// <returns>Current wheel RPM, or 0 if index is invalid.</returns>
        public float GetWheelRpm(int index)
        {
            if (allWheels == null || index < 0 || index >= allWheels.Length)
                return 0f;

            return allWheels[index].rpm;
        }
    }
}
