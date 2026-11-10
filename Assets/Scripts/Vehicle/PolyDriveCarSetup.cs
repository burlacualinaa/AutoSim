// PolyDriveCarSetup.cs — Integrates the PolyDrive Bavaria Sport car into AutoSim.
// Automatically wires up physics (WheelColliders, Rigidbody), visual wheel sync,
// 1:1 interior steering wheel rotation, driver seat anchor, camera rig, and input systems.
using System.Collections.Generic;
using UnityEngine;
using AutoSim.Input;
using AutoSim.UI;
using AutoSim.XR;
using AutoSim.Networking;

namespace AutoSim.Vehicle
{
    /// <summary>
    /// Configures the imported PolyDrive 'Bavaria_Sport' car model to be the active driving vehicle.
    /// Disables the placeholder vehicle, sets up WheelColliders, syncs visual wheels (LOD0-3),
    /// rotates the interior steering wheel with Logitech G920, and sets up driver cockpit camera.
    /// </summary>
    [AddComponentMenu("AutoSim/Vehicle/PolyDrive Car Setup")]
    public class PolyDriveCarSetup : MonoBehaviour
    {
        [Header("Wheel Calibration")]
        [Tooltip("Radius of the physical wheel (metres).")]
        [SerializeField] private float wheelRadius = 0.34f;

        [Tooltip("Suspension spring stiffness.")]
        [SerializeField] private float suspensionSpring = 36000f;

        [Tooltip("Suspension damper.")]
        [SerializeField] private float suspensionDamper = 4500f;

        [Tooltip("Suspension travel distance (metres).")]
        [SerializeField] private float suspensionDistance = 0.16f;

        [Header("Driver View & Steering")]
        [Tooltip("Driver eye anchor position relative to this car (X=left/right, Y=height, Z=forward/back).")]
        [SerializeField] private Vector3 driverSeatOffset = new Vector3(-0.364f, 1.12f, 0.05f);

        [Tooltip("Maximum steering wheel rotation degrees lock-to-lock.")]
        [SerializeField] private float maxSteerDegrees = 450f;

        // Cached wheel references
        private WheelCollider colFL;
        private WheelCollider colFR;
        private WheelCollider colRL;
        private WheelCollider colRR;

        private List<Transform> meshFL = new List<Transform>();
        private List<Transform> meshFR = new List<Transform>();
        private List<Transform> meshRL = new List<Transform>();
        private List<Transform> meshRR = new List<Transform>();

        private Transform steeringWheelTransform;
        private Quaternion initialSteerRot;

        private VehicleController vehicleController;

        private void Awake()
        {
            SetupPolyDriveCar();
        }

        private void Start()
        {
            SetupPolyDriveCar();
        }

        [ContextMenu("Setup PolyDrive Bavaria Car")]
        public void SetupPolyDriveCar()
        {
            // 1. Disable old placeholder box "Vehicle"
            GameObject oldVehicle = GameObject.Find("Vehicle");
            if (oldVehicle != null && oldVehicle != gameObject)
            {
                oldVehicle.SetActive(false);
                Debug.Log("[PolyDriveCarSetup] Disabled old placeholder Vehicle.");
            }

            // 2. Setup Rigidbody
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 1580f;
            rb.drag = 0.05f;
            rb.angularDrag = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // 3. Setup Chassis BoxCollider
            BoxCollider col = GetComponent<BoxCollider>();
            if (col == null) col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.72f, 0.10f);
            col.size = new Vector3(1.88f, 0.72f, 4.65f);

            // 4. Find Wheel Meshes across all LODs
            FindWheelMeshes();

            // 5. Create or configure WheelColliders
            EnsureWheelColliders();

            // 6. Setup VehicleController
            vehicleController = GetComponent<VehicleController>();
            if (vehicleController == null)
                vehicleController = gameObject.AddComponent<VehicleController>();

            vehicleController.SetWheelColliders(colFL, colFR, colRL, colRR);

            // 7. Input Providers
            KeyboardInputProvider keyboard = GetComponent<KeyboardInputProvider>();
            if (keyboard == null) keyboard = gameObject.AddComponent<KeyboardInputProvider>();

            LogitechG920InputProvider g920 = GetComponent<LogitechG920InputProvider>();
            if (g920 == null) g920 = gameObject.AddComponent<LogitechG920InputProvider>();
            g920.KeyboardFallback = keyboard;

            vehicleController.ActiveInputProvider = g920;
            vehicleController.InputProviderSource = g920;

            // 8. HoloLens 2 Networking
            if (GetComponent<UDPInputReceiver>() == null) gameObject.AddComponent<UDPInputReceiver>();
            if (GetComponent<NetworkInputProvider>() == null) gameObject.AddComponent<NetworkInputProvider>();
            if (GetComponent<NetworkManager>() == null) gameObject.AddComponent<NetworkManager>();

            // 9. Speedometer & Digital Cockpit UI
            if (GetComponent<Speedometer>() == null) gameObject.AddComponent<Speedometer>();
            if (GetComponent<SpeedometerUI>() == null) gameObject.AddComponent<SpeedometerUI>();

            // 10. Find Interior SteeringWheel
            FindSteeringWheel();

            // 11. Create Driver Seat Anchor for Camera
            CreateDriverSeatAnchor();

            // 12. Connect Main Camera
            ConnectCamera();

            Debug.Log("[PolyDriveCarSetup] PolyDrive Bavaria Sport car fully configured and ready to drive!");
        }

        private void FindWheelMeshes()
        {
            meshFL.Clear();
            meshFR.Clear();
            meshRL.Clear();
            meshRR.Clear();

            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            foreach (var t in allChildren)
            {
                string n = t.name.ToUpperInvariant();
                if (n.Contains("WHEEL_FL")) meshFL.Add(t);
                else if (n.Contains("WHEEL_FR")) meshFR.Add(t);
                else if (n.Contains("WHEEL_RL")) meshRL.Add(t);
                else if (n.Contains("WHEEL_RR")) meshRR.Add(t);
            }
        }

        private void EnsureWheelColliders()
        {
            // Standard positions for Bavaria Sport
            Vector3 posFL = new Vector3(-0.846f, 0.359f, 1.520f);
            Vector3 posFR = new Vector3(0.846f, 0.359f, 1.520f);
            Vector3 posRL = new Vector3(-0.846f, 0.359f, -1.328f);
            Vector3 posRR = new Vector3(0.846f, 0.359f, -1.328f);

            colFL = GetOrCreateWheelCollider("WheelCollider_FL", posFL);
            colFR = GetOrCreateWheelCollider("WheelCollider_FR", posFR);
            colRL = GetOrCreateWheelCollider("WheelCollider_RL", posRL);
            colRR = GetOrCreateWheelCollider("WheelCollider_RR", posRR);
        }

        private WheelCollider GetOrCreateWheelCollider(string colName, Vector3 localPos)
        {
            Transform existing = transform.Find(colName);
            if (existing != null)
            {
                var wc = existing.GetComponent<WheelCollider>();
                if (wc != null) return wc;
            }

            GameObject go = new GameObject(colName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;

            WheelCollider wheelCol = go.AddComponent<WheelCollider>();
            wheelCol.radius = wheelRadius;
            wheelCol.suspensionDistance = suspensionDistance;
            wheelCol.mass = 25f;

            JointSpring spring = wheelCol.suspensionSpring;
            spring.spring = suspensionSpring;
            spring.damper = suspensionDamper;
            spring.targetPosition = 0.5f;
            wheelCol.suspensionSpring = spring;

            WheelFrictionCurve fFriction = wheelCol.forwardFriction;
            fFriction.extremumSlip = 0.2f;
            fFriction.extremumValue = 1.0f;
            fFriction.asymptoteSlip = 0.5f;
            fFriction.asymptoteValue = 0.75f;
            fFriction.stiffness = 1.5f;
            wheelCol.forwardFriction = fFriction;

            WheelFrictionCurve sFriction = wheelCol.sidewaysFriction;
            sFriction.extremumSlip = 0.2f;
            sFriction.extremumValue = 1.0f;
            sFriction.asymptoteSlip = 0.5f;
            sFriction.asymptoteValue = 0.75f;
            sFriction.stiffness = 1.5f;
            wheelCol.sidewaysFriction = sFriction;

            return wheelCol;
        }

        private void FindSteeringWheel()
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (var t in children)
            {
                if (t.name.Equals("SteeringWheel", System.StringComparison.OrdinalIgnoreCase))
                {
                    steeringWheelTransform = t;
                    initialSteerRot = t.localRotation;
                    break;
                }
            }
        }

        private void CreateDriverSeatAnchor()
        {
            Transform anchor = transform.Find("DriverSeatAnchor");
            if (anchor == null)
            {
                GameObject anchorObj = new GameObject("DriverSeatAnchor");
                anchorObj.transform.SetParent(transform, false);
                anchorObj.transform.localPosition = driverSeatOffset;
                anchorObj.transform.localRotation = Quaternion.identity;
                anchor = anchorObj.transform;
            }
            else
            {
                anchor.localPosition = driverSeatOffset;
            }
        }

        private void ConnectCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = FindObjectOfType<Camera>();
            if (cam == null) return;

            // SimpleFollowCamera target
            var followCam = cam.GetComponent<SimpleFollowCamera>();
            if (followCam != null)
            {
                followCam.Target = transform;
            }

            // XRRigSetup
            var xr = cam.GetComponent<XRRigSetup>();
            if (xr != null)
            {
                xr.SetEditorCockpitView(true);
            }
        }

        private void Update()
        {
            // Sync all visual wheel meshes with physics WheelColliders
            SyncWheels(colFL, meshFL);
            SyncWheels(colFR, meshFR);
            SyncWheels(colRL, meshRL);
            SyncWheels(colRR, meshRR);

            // Sync 3D interior SteeringWheel rotation
            if (steeringWheelTransform != null && vehicleController != null)
            {
                float steer = 0f;
                if (vehicleController.ActiveInputProvider != null)
                    steer = vehicleController.ActiveInputProvider.Steering;

                float steerAngle = -steer * maxSteerDegrees;
                // Rotate steering wheel along its steering axis
                steeringWheelTransform.localRotation = initialSteerRot * Quaternion.Euler(0f, 0f, steerAngle);
            }
        }

        private void SyncWheels(WheelCollider col, List<Transform> meshes)
        {
            if (col == null || meshes == null || meshes.Count == 0) return;

            Vector3 worldPos;
            Quaternion worldRot;
            col.GetWorldPose(out worldPos, out worldRot);

            for (int i = 0; i < meshes.Count; i++)
            {
                var m = meshes[i];
                if (m != null)
                {
                    m.position = worldPos;
                    // Preserve the initial -90 FBX orientation while applying wheel roll and steer
                    m.rotation = worldRot * Quaternion.Euler(0f, 0f, 0f);
                }
            }
        }
    }
}
