// CockpitSetup.cs — High-fidelity procedural cockpit and vehicle cabin for AutoSim.
// Provides realistic interior (dashboard, dynamic 1:1 steering wheel, sports seats, center console,
// infotainment screen, rearview mirror, A-pillars) and exterior hood/windshield visible from inside.
using UnityEngine;
using AutoSim.Vehicle;
using AutoSim.Input;

namespace AutoSim.Environment
{
    /// <summary>
    /// Generates a realistic virtual car cockpit and interior around the driver's head position.
    /// Features a real-time rotating 3D steering wheel synced 1:1 with the physical Logitech G920,
    /// sculpted dashboard, center console with screen, sports bucket seats, rearview mirror,
    /// and exterior hood visible through the windshield.
    /// </summary>
    [AddComponentMenu("AutoSim/Environment/Cockpit Setup")]
    public class CockpitSetup : MonoBehaviour
    {
        // ──────────────────────────── Inspector Parameters ────────────────────────────

        [Header("Generation Settings")]
        [Tooltip("Automatically generate the cockpit on scene start.")]
        [SerializeField] private bool generateOnStart = true;

        [Header("Driver Seat Anchor")]
        [Tooltip("Position of the driver's eyes relative to vehicle origin (X=left, Y=height, Z=forward).")]
        [SerializeField] private Vector3 driverSeatOffset = new Vector3(-0.38f, 1.16f, -0.65f);

        [Header("Steering Wheel Settings")]
        [Tooltip("Radius of the steering wheel rim (metres).")]
        [SerializeField] private float wheelRadius = 0.165f;

        [Tooltip("Tube thickness of the steering wheel rim (metres).")]
        [SerializeField] private float wheelTubeRadius = 0.016f;

        [Tooltip("Tilt angle of the steering column towards the driver (degrees).")]
        [SerializeField] private float wheelTiltAngle = 22f;

        [Tooltip("Maximum steering rotation in degrees lock-to-lock (e.g. 450 for 900-degree wheel).")]
        [SerializeField] private float maxWheelRotationDegrees = 450f;

        [Header("Exterior Colors & Styling")]
        [SerializeField] private Color exteriorPaintColor = new Color(0.08f, 0.22f, 0.55f); // Deep metallic sports blue
        [SerializeField] private Color interiorTrimColor = new Color(0.12f, 0.12f, 0.14f);  // Dark charcoal leather/plastic
        [SerializeField] private Color accentSilverColor = new Color(0.75f, 0.77f, 0.80f);  // Brushed aluminum accents

        // ──────────────────────────── Runtime References ──────────────────────────────

        private Transform _cockpitContainer;
        private Transform _wheelRotator;
        private VehicleController _vehicleController;
        private IInputProvider _inputProvider;

        // ──────────────────────────── Unity Lifecycle ─────────────────────────────────

        private void Start()
        {
            _vehicleController = GetComponent<VehicleController>();
            if (_vehicleController == null)
                _vehicleController = GetComponentInParent<VehicleController>();

            _inputProvider = GetComponent<IInputProvider>();
            if (_inputProvider == null)
                _inputProvider = GetComponentInParent<IInputProvider>();

            if (generateOnStart)
            {
                GenerateCockpit();
            }
        }

        private void Update()
        {
            // Sync virtual steering wheel rotation in 1:1 real-time with physical wheel / controller
            if (_wheelRotator != null)
            {
                float steerFraction = 0f;

                if (_vehicleController != null && _vehicleController.ActiveInputProvider != null)
                {
                    steerFraction = _vehicleController.ActiveInputProvider.Steering;
                }
                else if (_inputProvider != null)
                {
                    steerFraction = _inputProvider.Steering;
                }

                // Smoothly rotate the wheel around its tilted steering column axis
                float targetAngle = -steerFraction * maxWheelRotationDegrees;
                _wheelRotator.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
            }
        }

        // ──────────────────────────── Public API ──────────────────────────────────────

        /// <summary>
        /// Generates all realistic interior and exterior cockpit elements.
        /// Safe to call multiple times (clears previous objects).
        /// </summary>
        [ContextMenu("Generate Cockpit")]
        public void GenerateCockpit()
        {
            ClearCockpit();

            // Replace or adjust old solid cabin cube so it doesn't block the driver's interior view
            Transform oldCabin = transform.Find("Body_Cabin");
            if (oldCabin != null)
            {
                var r = oldCabin.GetComponent<MeshRenderer>();
                if (r != null) r.enabled = false;
            }

            _cockpitContainer = new GameObject("Cockpit_Interior").transform;
            _cockpitContainer.SetParent(transform, false);
            _cockpitContainer.localPosition = Vector3.zero;
            _cockpitContainer.localRotation = Quaternion.identity;

            // 1. Exterior elements visible from inside (Hood, Windshield, A-Pillars)
            CreateExteriorHood();
            CreateWindshieldAndPillars();

            // 2. Interior Dashboard and Center Console
            CreateDashboard();
            CreateCenterConsole();

            // 3. Realistic 3D Steering Wheel with real-time rotation
            CreateSteeringWheel();

            // 4. Driver and Passenger Sports Bucket Seats
            CreateSeats();

            // 5. Interior Door Panels (Cards) and Armrests
            CreateInteriorDoorCards();

            // 6. Rearview and Side Mirrors
            CreateMirrors();

            Debug.Log("[CockpitSetup] Realistic automotive cockpit generated successfully.");
        }

        /// <summary>Destroys all previously generated cockpit objects.</summary>
        [ContextMenu("Clear Cockpit")]
        public void ClearCockpit()
        {
            if (_cockpitContainer != null)
            {
                if (Application.isPlaying)
                    Destroy(_cockpitContainer.gameObject);
                else
                    DestroyImmediate(_cockpitContainer.gameObject);
                _cockpitContainer = null;
            }

            Transform existing = transform.Find("Cockpit_Interior");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
        }

        // ──────────────────────────── 1. Exterior Hood & Windshield ───────────────────

        private void CreateExteriorHood()
        {
            Material paintMat = CreateMaterial(exteriorPaintColor, metallic: 0.85f, smoothness: 0.8f);

            // Sculpted hood stretching forward in front of driver
            GameObject hood = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hood.name = "Exterior_Hood";
            hood.transform.SetParent(_cockpitContainer, false);
            hood.transform.localPosition = new Vector3(0f, 0.78f, 1.15f);
            hood.transform.localScale = new Vector3(1.72f, 0.12f, 1.85f);
            hood.transform.localRotation = Quaternion.Euler(-3.5f, 0f, 0f); // slight aerodynamic slope
            DestroyCollider(hood);
            hood.GetComponent<MeshRenderer>().sharedMaterial = paintMat;

            // Front cowl / wiper tray (black matte)
            GameObject cowl = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cowl.name = "Exterior_Cowl";
            cowl.transform.SetParent(_cockpitContainer, false);
            cowl.transform.localPosition = new Vector3(0f, 0.86f, 0.22f);
            cowl.transform.localScale = new Vector3(1.68f, 0.08f, 0.22f);
            DestroyCollider(cowl);
            cowl.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial(new Color(0.06f, 0.06f, 0.07f), 0.1f, 0.2f);
        }

        private void CreateWindshieldAndPillars()
        {
            Material frameMat = CreateMaterial(new Color(0.08f, 0.08f, 0.09f), metallic: 0.3f, smoothness: 0.5f);

            // Left A-Pillar (slanted driver pillar)
            GameObject pillarL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillarL.name = "A_Pillar_Left";
            pillarL.transform.SetParent(_cockpitContainer, false);
            pillarL.transform.localPosition = new Vector3(-0.76f, 1.18f, 0.05f);
            pillarL.transform.localScale = new Vector3(0.09f, 0.09f, 0.95f);
            pillarL.transform.localRotation = Quaternion.Euler(-42f, 0f, 12f);
            DestroyCollider(pillarL);
            pillarL.GetComponent<MeshRenderer>().sharedMaterial = frameMat;

            // Right A-Pillar
            GameObject pillarR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillarR.name = "A_Pillar_Right";
            pillarR.transform.SetParent(_cockpitContainer, false);
            pillarR.transform.localPosition = new Vector3(0.76f, 1.18f, 0.05f);
            pillarR.transform.localScale = new Vector3(0.09f, 0.09f, 0.95f);
            pillarR.transform.localRotation = Quaternion.Euler(-42f, 0f, -12f);
            DestroyCollider(pillarR);
            pillarR.GetComponent<MeshRenderer>().sharedMaterial = frameMat;

            // Upper roof windshield header
            GameObject roofHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roofHeader.name = "Roof_Header";
            roofHeader.transform.SetParent(_cockpitContainer, false);
            roofHeader.transform.localPosition = new Vector3(0f, 1.48f, -0.32f);
            roofHeader.transform.localScale = new Vector3(1.52f, 0.08f, 0.25f);
            DestroyCollider(roofHeader);
            roofHeader.GetComponent<MeshRenderer>().sharedMaterial = frameMat;

            // Transparent tinted windshield glass (allows clear road visibility)
            GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glass.name = "Windshield_Glass";
            glass.transform.SetParent(_cockpitContainer, false);
            glass.transform.localPosition = new Vector3(0f, 1.16f, 0.02f);
            glass.transform.localScale = new Vector3(1.48f, 0.015f, 0.95f);
            glass.transform.localRotation = Quaternion.Euler(-42f, 0f, 0f);
            DestroyCollider(glass);

            Material glassMat = CreateTransparentMaterial(new Color(0.85f, 0.92f, 0.98f, 0.12f), metallic: 0.9f, smoothness: 0.95f);
            glass.GetComponent<MeshRenderer>().sharedMaterial = glassMat;
        }

        // ──────────────────────────── 2. Dashboard & Center Console ──────────────────

        private void CreateDashboard()
        {
            Material dashLeatherMat = CreateMaterial(interiorTrimColor, metallic: 0.05f, smoothness: 0.25f);
            Material silverTrimMat = CreateMaterial(accentSilverColor, metallic: 0.9f, smoothness: 0.85f);

            // Main dashboard horizontal deck
            GameObject mainDash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mainDash.name = "Dashboard_Main";
            mainDash.transform.SetParent(_cockpitContainer, false);
            mainDash.transform.localPosition = new Vector3(0f, 0.84f, 0.05f);
            mainDash.transform.localScale = new Vector3(1.65f, 0.28f, 0.58f);
            DestroyCollider(mainDash);
            mainDash.GetComponent<MeshRenderer>().sharedMaterial = dashLeatherMat;

            // Driver instrument binnacle cowl (curves over the gauge cluster behind the wheel)
            GameObject cowl = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cowl.name = "Instrument_Cowl";
            cowl.transform.SetParent(_cockpitContainer, false);
            cowl.transform.localPosition = new Vector3(driverSeatOffset.x, 0.96f, -0.05f);
            cowl.transform.localScale = new Vector3(0.48f, 0.16f, 0.36f);
            cowl.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            DestroyCollider(cowl);
            cowl.GetComponent<MeshRenderer>().sharedMaterial = dashLeatherMat;

            // Metallic decorative trim strip across dash
            GameObject trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trim.name = "Dashboard_SilverTrim";
            trim.transform.SetParent(_cockpitContainer, false);
            trim.transform.localPosition = new Vector3(0f, 0.82f, -0.22f);
            trim.transform.localScale = new Vector3(1.62f, 0.025f, 0.04f);
            DestroyCollider(trim);
            trim.GetComponent<MeshRenderer>().sharedMaterial = silverTrimMat;

            // Air vents (left, center, right)
            CreateAirVent("Vent_Left", new Vector3(-0.70f, 0.86f, -0.21f));
            CreateAirVent("Vent_CenterLeft", new Vector3(-0.10f, 0.86f, -0.21f));
            CreateAirVent("Vent_CenterRight", new Vector3(0.10f, 0.86f, -0.21f));
            CreateAirVent("Vent_Right", new Vector3(0.70f, 0.86f, -0.21f));
        }

        private void CreateAirVent(string name, Vector3 pos)
        {
            GameObject vent = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vent.name = name;
            vent.transform.SetParent(_cockpitContainer, false);
            vent.transform.localPosition = pos;
            vent.transform.localScale = new Vector3(0.12f, 0.05f, 0.02f);
            DestroyCollider(vent);
            vent.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial(new Color(0.04f, 0.04f, 0.04f), 0.5f, 0.5f);
        }

        private void CreateCenterConsole()
        {
            Material darkMat = CreateMaterial(interiorTrimColor, metallic: 0.1f, smoothness: 0.3f);
            Material screenMat = CreateMaterial(new Color(0.05f, 0.12f, 0.22f), metallic: 0.4f, smoothness: 0.9f);

            // Center stack sloping downward between driver and passenger
            GameObject stack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stack.name = "Center_Stack";
            stack.transform.SetParent(_cockpitContainer, false);
            stack.transform.localPosition = new Vector3(0f, 0.62f, -0.32f);
            stack.transform.localScale = new Vector3(0.38f, 0.32f, 0.52f);
            stack.transform.localRotation = Quaternion.Euler(24f, 0f, 0f);
            DestroyCollider(stack);
            stack.GetComponent<MeshRenderer>().sharedMaterial = darkMat;

            // Infotainment / Navigation Screen
            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "Infotainment_Display";
            screen.transform.SetParent(_cockpitContainer, false);
            screen.transform.localPosition = new Vector3(0f, 0.86f, -0.16f);
            screen.transform.localScale = new Vector3(0.32f, 0.18f, 0.025f);
            screen.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            DestroyCollider(screen);
            screen.GetComponent<MeshRenderer>().sharedMaterial = screenMat;

            // Center transmission tunnel & armrest extending between seats
            GameObject armrest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            armrest.name = "Center_Armrest";
            armrest.transform.SetParent(_cockpitContainer, false);
            armrest.transform.localPosition = new Vector3(0f, 0.48f, -0.80f);
            armrest.transform.localScale = new Vector3(0.32f, 0.36f, 0.75f);
            DestroyCollider(armrest);
            armrest.GetComponent<MeshRenderer>().sharedMaterial = darkMat;

            // Gear Shifter base & stick
            GameObject shifterStick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shifterStick.name = "Gear_Shifter";
            shifterStick.transform.SetParent(_cockpitContainer, false);
            shifterStick.transform.localPosition = new Vector3(0f, 0.64f, -0.52f);
            shifterStick.transform.localScale = new Vector3(0.035f, 0.08f, 0.035f);
            DestroyCollider(shifterStick);
            shifterStick.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial(accentSilverColor, 0.95f, 0.9f);

            // Shifter Knob
            GameObject knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knob.name = "Shifter_Knob";
            knob.transform.SetParent(_cockpitContainer, false);
            knob.transform.localPosition = new Vector3(0f, 0.72f, -0.52f);
            knob.transform.localScale = new Vector3(0.065f, 0.065f, 0.065f);
            DestroyCollider(knob);
            knob.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial(new Color(0.04f, 0.04f, 0.04f), 0.2f, 0.6f);
        }

        // ──────────────────────────── 3. Realistic 3D Steering Wheel ──────────────────

        private void CreateSteeringWheel()
        {
            // Position wheel column aligned directly in front of the driver seat
            Vector3 wheelBasePos = new Vector3(driverSeatOffset.x, 0.82f, -0.28f);

            // Static Steering Column
            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "Steering_Column";
            column.transform.SetParent(_cockpitContainer, false);
            column.transform.localPosition = wheelBasePos + new Vector3(0f, -0.05f, 0.12f);
            column.transform.localScale = new Vector3(0.09f, 0.15f, 0.09f);
            column.transform.localRotation = Quaternion.Euler(90f + wheelTiltAngle, 0f, 0f);
            DestroyCollider(column);
            column.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial(new Color(0.06f, 0.06f, 0.06f), 0.1f, 0.3f);

            // Base Pivot for the wheel (holds the tilt angle)
            GameObject wheelPivotObj = new GameObject("SteeringWheel_Pivot");
            wheelPivotObj.transform.SetParent(_cockpitContainer, false);
            wheelPivotObj.transform.localPosition = wheelBasePos;
            wheelPivotObj.transform.localRotation = Quaternion.Euler(wheelTiltAngle, 0f, 0f);

            // Dynamic Rotating Transform (rotates around Z-axis matching Logitech G920 1:1)
            GameObject rotatorObj = new GameObject("Wheel_Rotator");
            rotatorObj.transform.SetParent(wheelPivotObj.transform, false);
            rotatorObj.transform.localPosition = Vector3.zero;
            rotatorObj.transform.localRotation = Quaternion.identity;
            _wheelRotator = rotatorObj.transform;

            Material rimLeatherMat = CreateMaterial(new Color(0.04f, 0.04f, 0.04f), metallic: 0.1f, smoothness: 0.2f);
            Material spokeMat = CreateMaterial(accentSilverColor, metallic: 0.9f, smoothness: 0.85f);
            Material emblemMat = CreateMaterial(new Color(0.08f, 0.35f, 0.85f), metallic: 0.8f, smoothness: 0.9f);

            // Center Hub / Horn Button
            GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hub.name = "Wheel_Hub";
            hub.transform.SetParent(_wheelRotator, false);
            hub.transform.localPosition = Vector3.zero;
            hub.transform.localScale = new Vector3(0.085f, 0.02f, 0.085f);
            hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            DestroyCollider(hub);
            hub.GetComponent<MeshRenderer>().sharedMaterial = spokeMat;

            // Center Badge
            GameObject badge = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            badge.name = "Wheel_Badge";
            badge.transform.SetParent(_wheelRotator, false);
            badge.transform.localPosition = new Vector3(0f, 0f, -0.012f);
            badge.transform.localScale = new Vector3(0.04f, 0.005f, 0.04f);
            badge.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            DestroyCollider(badge);
            badge.GetComponent<MeshRenderer>().sharedMaterial = emblemMat;

            // 3-Spoke Design (Left, Right, Bottom)
            CreateWheelSpoke("Spoke_Left", _wheelRotator, new Vector3(-wheelRadius * 0.48f, 0f, 0f), new Vector3(wheelRadius * 0.9f, 0.012f, 0.035f), spokeMat);
            CreateWheelSpoke("Spoke_Right", _wheelRotator, new Vector3(wheelRadius * 0.48f, 0f, 0f), new Vector3(wheelRadius * 0.9f, 0.012f, 0.035f), spokeMat);
            CreateWheelSpoke("Spoke_Bottom", _wheelRotator, new Vector3(0f, -wheelRadius * 0.48f, 0f), new Vector3(0.035f, 0.012f, wheelRadius * 0.9f), spokeMat);

            // Torus Wheel Rim (approximated with smooth segmented cylinders)
            int segments = 28;
            for (int i = 0; i < segments; i++)
            {
                float a0 = (360f / segments) * i * Mathf.Deg2Rad;
                float a1 = (360f / segments) * (i + 1) * Mathf.Deg2Rad;

                Vector3 p0 = new Vector3(Mathf.Cos(a0) * wheelRadius, Mathf.Sin(a0) * wheelRadius, 0f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * wheelRadius, Mathf.Sin(a1) * wheelRadius, 0f);

                Vector3 mid = (p0 + p1) * 0.5f;
                float length = Vector3.Distance(p0, p1);

                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                seg.name = $"RimSeg_{i}";
                seg.transform.SetParent(_wheelRotator, false);
                seg.transform.localPosition = mid;
                seg.transform.localScale = new Vector3(wheelTubeRadius * 2f, length * 0.5f, wheelTubeRadius * 2f);
                seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (p1 - p0).normalized);
                DestroyCollider(seg);
                seg.GetComponent<MeshRenderer>().sharedMaterial = rimLeatherMat;
            }

            // Paddle shifters behind wheel (Left downshift, Right upshift)
            CreatePaddleShifter("Paddle_Left", wheelPivotObj.transform, new Vector3(-0.13f, 0.04f, 0.04f));
            CreatePaddleShifter("Paddle_Right", wheelPivotObj.transform, new Vector3(0.13f, 0.04f, 0.04f));
        }

        private void CreateWheelSpoke(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject spoke = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spoke.name = name;
            spoke.transform.SetParent(parent, false);
            spoke.transform.localPosition = localPos;
            spoke.transform.localScale = localScale;
            DestroyCollider(spoke);
            spoke.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private void CreatePaddleShifter(string name, Transform parent, Vector3 localPos)
        {
            GameObject paddle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paddle.name = name;
            paddle.transform.SetParent(parent, false);
            paddle.transform.localPosition = localPos;
            paddle.transform.localScale = new Vector3(0.04f, 0.09f, 0.008f);
            DestroyCollider(paddle);
            paddle.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial(accentSilverColor, 0.95f, 0.85f);
        }

        // ──────────────────────────── 4. Sports Bucket Seats ──────────────────────────

        private void CreateSeats()
        {
            Material seatMat = CreateMaterial(new Color(0.11f, 0.11f, 0.13f), metallic: 0.1f, smoothness: 0.35f);

            // Driver Seat (directly underneath driver anchor)
            CreateBucketSeat("Driver_Seat", new Vector3(driverSeatOffset.x, 0.35f, driverSeatOffset.z), seatMat);

            // Passenger Seat
            CreateBucketSeat("Passenger_Seat", new Vector3(-driverSeatOffset.x, 0.35f, driverSeatOffset.z), seatMat);
        }

        private void CreateBucketSeat(string name, Vector3 basePos, Material mat)
        {
            GameObject seatGroup = new GameObject(name);
            seatGroup.transform.SetParent(_cockpitContainer, false);
            seatGroup.transform.localPosition = basePos;

            // Seat base cushion
            GameObject cushion = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cushion.name = "Cushion";
            cushion.transform.SetParent(seatGroup.transform, false);
            cushion.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            cushion.transform.localScale = new Vector3(0.48f, 0.14f, 0.52f);
            DestroyCollider(cushion);
            cushion.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Seat backrest (reclined slightly)
            GameObject backrest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backrest.name = "Backrest";
            backrest.transform.SetParent(seatGroup.transform, false);
            backrest.transform.localPosition = new Vector3(0f, 0.38f, -0.12f);
            backrest.transform.localScale = new Vector3(0.46f, 0.65f, 0.12f);
            backrest.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            DestroyCollider(backrest);
            backrest.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Headrest
            GameObject headrest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            headrest.name = "Headrest";
            headrest.transform.SetParent(seatGroup.transform, false);
            headrest.transform.localPosition = new Vector3(0f, 0.78f, -0.22f);
            headrest.transform.localScale = new Vector3(0.24f, 0.20f, 0.10f);
            DestroyCollider(headrest);
            headrest.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ──────────────────────────── 5. Door Cards & Armrests ────────────────────────

        private void CreateInteriorDoorCards()
        {
            Material cardMat = CreateMaterial(interiorTrimColor, 0.1f, 0.3f);
            Material handleMat = CreateMaterial(accentSilverColor, 0.9f, 0.85f);

            // Left Door Card (next to driver)
            CreateDoorCard("DoorCard_Left", -0.80f, cardMat, handleMat);

            // Right Door Card (next to passenger)
            CreateDoorCard("DoorCard_Right", 0.80f, cardMat, handleMat);
        }

        private void CreateDoorCard(string name, float xOffset, Material cardMat, Material handleMat)
        {
            GameObject door = new GameObject(name);
            door.transform.SetParent(_cockpitContainer, false);
            door.transform.localPosition = new Vector3(xOffset, 0.68f, -0.55f);

            // Door panel
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            panel.transform.SetParent(door.transform, false);
            panel.transform.localScale = new Vector3(0.08f, 0.52f, 1.15f);
            DestroyCollider(panel);
            panel.GetComponent<MeshRenderer>().sharedMaterial = cardMat;

            // Armrest on door
            GameObject armrest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            armrest.name = "Armrest";
            armrest.transform.SetParent(door.transform, false);
            armrest.transform.localPosition = new Vector3(xOffset > 0 ? -0.05f : 0.05f, 0.05f, 0f);
            armrest.transform.localScale = new Vector3(0.06f, 0.08f, 0.45f);
            DestroyCollider(armrest);
            armrest.GetComponent<MeshRenderer>().sharedMaterial = cardMat;

            // Interior Door Handle (metallic)
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Handle";
            handle.transform.SetParent(door.transform, false);
            handle.transform.localPosition = new Vector3(xOffset > 0 ? -0.05f : 0.05f, 0.14f, 0.22f);
            handle.transform.localScale = new Vector3(0.03f, 0.035f, 0.11f);
            DestroyCollider(handle);
            handle.GetComponent<MeshRenderer>().sharedMaterial = handleMat;
        }

        // ──────────────────────────── 6. Mirrors ──────────────────────────────────────

        private void CreateMirrors()
        {
            Material mirrorMat = CreateMaterial(new Color(0.85f, 0.90f, 0.95f), metallic: 0.98f, smoothness: 0.98f);
            Material housingMat = CreateMaterial(new Color(0.05f, 0.05f, 0.05f), 0.2f, 0.5f);

            // Interior Rearview Mirror (mounted at top center of windshield)
            GameObject rMirror = new GameObject("Rearview_Mirror");
            rMirror.transform.SetParent(_cockpitContainer, false);
            rMirror.transform.localPosition = new Vector3(0f, 1.38f, -0.22f);
            rMirror.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);

            GameObject rHousing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rHousing.name = "Housing";
            rHousing.transform.SetParent(rMirror.transform, false);
            rHousing.transform.localScale = new Vector3(0.24f, 0.065f, 0.02f);
            DestroyCollider(rHousing);
            rHousing.GetComponent<MeshRenderer>().sharedMaterial = housingMat;

            GameObject rGlass = GameObject.CreatePrimitive(PrimitiveType.Quad);
            rGlass.name = "Glass";
            rGlass.transform.SetParent(rMirror.transform, false);
            rGlass.transform.localPosition = new Vector3(0f, 0f, -0.011f);
            rGlass.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            rGlass.transform.localScale = new Vector3(0.23f, 0.055f, 1f);
            DestroyCollider(rGlass);
            rGlass.GetComponent<MeshRenderer>().sharedMaterial = mirrorMat;

            // Exterior Driver Side Mirror
            CreateSideMirror("SideMirror_Left", new Vector3(-0.92f, 0.98f, 0.10f), Quaternion.Euler(0f, 18f, 0f), housingMat, mirrorMat);
            // Exterior Passenger Side Mirror
            CreateSideMirror("SideMirror_Right", new Vector3(0.92f, 0.98f, 0.10f), Quaternion.Euler(0f, -18f, 0f), housingMat, mirrorMat);
        }

        private void CreateSideMirror(string name, Vector3 pos, Quaternion rot, Material housingMat, Material mirrorMat)
        {
            GameObject m = new GameObject(name);
            m.transform.SetParent(_cockpitContainer, false);
            m.transform.localPosition = pos;
            m.transform.localRotation = rot;

            GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            housing.name = "Housing";
            housing.transform.SetParent(m.transform, false);
            housing.transform.localScale = new Vector3(0.18f, 0.10f, 0.08f);
            DestroyCollider(housing);
            housing.GetComponent<MeshRenderer>().sharedMaterial = housingMat;

            GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glass.name = "Glass";
            glass.transform.SetParent(m.transform, false);
            glass.transform.localPosition = new Vector3(0f, 0f, -0.041f);
            glass.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            glass.transform.localScale = new Vector3(0.16f, 0.085f, 1f);
            DestroyCollider(glass);
            glass.GetComponent<MeshRenderer>().sharedMaterial = mirrorMat;
        }

        // ──────────────────────────── Helper Utilities ────────────────────────────────

        private static Material CreateMaterial(Color color, float metallic = 0f, float smoothness = 0.5f)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", smoothness);
            return mat;
        }

        private static Material CreateTransparentMaterial(Color color, float metallic = 0.8f, float smoothness = 0.9f)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", smoothness);
            mat.SetFloat("_Mode", 3f); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            return mat;
        }

        private static void DestroyCollider(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
        }
    }
}
