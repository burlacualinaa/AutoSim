// XRRigSetup.cs — Configures camera and driver view for HoloLens 2 AR and Editor.
using UnityEngine;
using AutoSim.Vehicle;

namespace AutoSim.XR
{
    /// <summary>
    /// Configures the camera rig for HoloLens 2 AR and Editor simulator driving.
    /// Anchors the camera to the vehicle's driver seat, enables optical pass-through,
    /// hides obstructions, and provides in-cockpit driver seat view toggle.
    /// </summary>
    [AddComponentMenu("AutoSim/XR/XR Rig Setup")]
    public class XRRigSetup : MonoBehaviour
    {
        [Header("Driver Seat Anchor")]
        [Tooltip("Offset of the driver's eyes relative to the vehicle (X=left/right, Y=height, Z=front/back).")]
        [SerializeField] private Vector3 driverSeatOffset = new Vector3(-0.364f, 1.25f, 0.20f);

        [Header("AR Pass-through Settings")]
        [Tooltip("Make camera background solid black (0,0,0,0) for HoloLens 2 transparent display.")]
        [SerializeField] private bool enableARPassthroughOnUWP = true;

        [Tooltip("Hide the solid roof/cabin cube on HoloLens so it doesn't obstruct vision.")]
        [SerializeField] private bool hideCabinOnHoloLens = true;

        [Header("Editor View Toggle")]
        [Tooltip("Default to first-person Cockpit view when entering Play mode in Editor.")]
        [SerializeField] private bool defaultToCockpitInEditor = true;

        [Tooltip("Key to toggle between Cockpit and Chase view in Unity Editor.")]
        [SerializeField] private KeyCode toggleViewKey = KeyCode.C;

        private bool isCockpitViewInEditor = false;

        private void Awake()
        {
#if UNITY_WSA && !UNITY_EDITOR
            SetupForHoloLens();
#endif
        }

        private void Start()
        {
#if UNITY_WSA && !UNITY_EDITOR
            SetupForHoloLens();
#elif UNITY_EDITOR
            if (defaultToCockpitInEditor)
            {
                SetEditorCockpitView(true);
            }
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (UnityEngine.Input.GetKeyDown(toggleViewKey))
            {
                SetEditorCockpitView(!isCockpitViewInEditor);
            }
#endif
        }

        public void SetEditorCockpitView(bool enableCockpit)
        {
            Camera cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            var followCam = cam.GetComponent<SimpleFollowCamera>();
            isCockpitViewInEditor = enableCockpit;

            if (isCockpitViewInEditor)
            {
                var vehicle = FindActiveVehicle();
                if (vehicle != null)
                {
                    Transform seatAnchor = vehicle.transform.Find("DriverSeatAnchor");
                    if (seatAnchor == null)
                    {
                        GameObject anchorObj = new GameObject("DriverSeatAnchor");
                        anchorObj.transform.SetParent(vehicle.transform, false);
                        anchorObj.transform.localPosition = driverSeatOffset;
                        anchorObj.transform.localRotation = Quaternion.identity;
                        seatAnchor = anchorObj.transform;
                    }

                    if (followCam != null) followCam.enabled = false;
                    cam.transform.SetParent(seatAnchor, false);
                    cam.transform.localPosition = Vector3.zero;
                    cam.transform.localRotation = Quaternion.identity;
                    Debug.Log($"[XRRigSetup] In-Car Driver Seat View Active on {vehicle.name}. Press [C] to switch to Chase Camera.");
                }
            }
            else
            {
                cam.transform.SetParent(null);
                if (followCam != null)
                {
                    var vehicle = FindActiveVehicle();
                    if (vehicle != null) followCam.Target = vehicle.transform;
                    followCam.enabled = true;
                }
                Debug.Log("[XRRigSetup] Chase Camera Active. Press [C] to switch to In-Car Driver Seat View.");
            }
        }

        /// <summary>
        /// Configures the camera and vehicle for seated HoloLens 2 AR driving.
        /// </summary>
        public void SetupForHoloLens()
        {
            Camera cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;

            if (cam != null)
            {
                if (enableARPassthroughOnUWP)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                }
                cam.nearClipPlane = 0.05f;
            }

            var vehicle = FindActiveVehicle();
            if (vehicle != null)
            {
                if (hideCabinOnHoloLens)
                {
                    Transform cabin = vehicle.transform.Find("Body_Cabin");
                    if (cabin != null)
                    {
                        var renderer = cabin.GetComponent<MeshRenderer>();
                        if (renderer != null) renderer.enabled = false;
                    }
                }

                Transform seatAnchor = vehicle.transform.Find("DriverSeatAnchor");
                if (seatAnchor == null)
                {
                    GameObject anchorObj = new GameObject("DriverSeatAnchor");
                    anchorObj.transform.SetParent(vehicle.transform, false);
                    anchorObj.transform.localPosition = driverSeatOffset;
                    anchorObj.transform.localRotation = Quaternion.identity;
                    seatAnchor = anchorObj.transform;
                }

                if (cam != null)
                {
                    var followCam = cam.GetComponent<SimpleFollowCamera>();
                    if (followCam != null) followCam.enabled = false;

                    cam.transform.SetParent(seatAnchor, false);
                    cam.transform.localPosition = Vector3.zero;
                    cam.transform.localRotation = Quaternion.identity;
                }

                Debug.Log($"[XRRigSetup] HoloLens 2 camera anchored to {vehicle.name} driver seat.");
            }
        }

        private VehicleController FindActiveVehicle()
        {
            var all = FindObjectsOfType<VehicleController>();
            foreach (var v in all)
            {
                if (v.gameObject.activeInHierarchy)
                    return v;
            }
            return all.Length > 0 ? all[0] : null;
        }
    }
}
