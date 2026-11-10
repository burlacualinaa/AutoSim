// Speedometer.cs — Provides processed speedometer/tachometer data from VehicleController.
// Use this as the single source-of-truth for UI and telemetry speed readouts.
using System;
using UnityEngine;

namespace AutoSim.Vehicle
{
    /// <summary>
    /// Reads <see cref="VehicleController"/> state and exposes speed, RPM, gear estimate,
    /// and fires events when speed changes significantly.
    /// </summary>
    [AddComponentMenu("AutoSim/Vehicle/Speedometer")]
    public class Speedometer : MonoBehaviour
    {
        // ---------------------------------------------------------------
        //  Serialized References
        // ---------------------------------------------------------------

        [Header("Vehicle Reference")]
        [Tooltip("The VehicleController to read speed data from.")]
        [SerializeField] private VehicleController vehicleController;

        [Header("Speed Change Threshold")]
        [Tooltip("Minimum speed delta (km/h) before OnSpeedChanged fires.")]
        [SerializeField] private float speedChangeThreshold = 0.5f;

        // ---------------------------------------------------------------
        //  Public Properties
        // ---------------------------------------------------------------

        /// <summary>Current speed in kilometres per hour.</summary>
        public float SpeedKmh { get; private set; }

        /// <summary>Current speed in miles per hour.</summary>
        public float SpeedMph { get; private set; }

        /// <summary>Speed normalised to 0–1 range based on <see cref="AutoSimConstants.MAX_SPEED_KMH"/>.</summary>
        public float NormalizedSpeed { get; private set; }

        /// <summary>Estimated engine RPM from the vehicle controller.</summary>
        public float Rpm { get; private set; }

        /// <summary>Estimated gear number (1–6) based on simple speed ranges.</summary>
        public int GearEstimate { get; private set; }

        // ---------------------------------------------------------------
        //  Events
        // ---------------------------------------------------------------

        /// <summary>
        /// Fired whenever the speed changes by more than <see cref="speedChangeThreshold"/> km/h
        /// compared to the last notified value. Parameter is the new speed in km/h.
        /// </summary>
        public event Action<float> OnSpeedChanged;

        // ---------------------------------------------------------------
        //  Internal State
        // ---------------------------------------------------------------

        private float lastNotifiedSpeed;

        // ---------------------------------------------------------------
        //  Unity Lifecycle
        // ---------------------------------------------------------------

        private void Awake()
        {
            if (vehicleController == null)
                vehicleController = GetComponent<VehicleController>();
        }

        private void Update()
        {
            if (vehicleController == null)
                return;

            // Read raw values
            SpeedKmh = vehicleController.SpeedKmh;
            SpeedMph = SpeedKmh * 0.621371f;
            NormalizedSpeed = Mathf.Clamp01(SpeedKmh / AutoSimConstants.MAX_SPEED_KMH);
            Rpm = vehicleController.Rpm;

            // Estimate gear from speed ranges
            GearEstimate = EstimateGear(SpeedKmh);

            // Fire event when speed changes significantly
            if (Mathf.Abs(SpeedKmh - lastNotifiedSpeed) >= speedChangeThreshold)
            {
                lastNotifiedSpeed = SpeedKmh;
                OnSpeedChanged?.Invoke(SpeedKmh);
            }
        }

        // ---------------------------------------------------------------
        //  Gear Estimation
        // ---------------------------------------------------------------

        /// <summary>
        /// Simple gear estimation from speed ranges (km/h).
        /// Assumes a 6-speed gearbox with roughly even ratio spacing.
        /// </summary>
        /// <param name="speedKmh">Current speed in km/h.</param>
        /// <returns>Gear number 1–6.</returns>
        private static int EstimateGear(float speedKmh)
        {
            // Approximate shift points for a typical sedan:
            //  1st: 0–20, 2nd: 20–40, 3rd: 40–70, 4th: 70–100, 5th: 100–140, 6th: 140+
            if (speedKmh < 20f) return 1;
            if (speedKmh < 40f) return 2;
            if (speedKmh < 70f) return 3;
            if (speedKmh < 100f) return 4;
            if (speedKmh < 140f) return 5;
            return 6;
        }
    }
}
