// XRRigSetup.cs — Configures camera and driver view for HoloLens 2 AR and Editor.
using UnityEngine;
using AutoSim.Vehicle;

namespace AutoSim.XR
{
    /// <summary>
    /// Configures the camera rig for HoloLens 2 AR.
    /// Anchors the HoloLens camera to the vehicle's driver seat, enables optical pass-through,
    /// hides the solid cabin box to prevent near-clipping in the user's eyes, and disables chase camera.
    /// </summary>
    [AddComponentMenu("AutoSim/XR/XR Rig Setup")]
    public class XRRigSetup : MonoBehaviour
    {
        [Header("Driver Seat Anchor")]
        [Tooltip("Offset of the driver's eyes relative to the vehicle (X=left/right, Y=height, Z=front/back).")]
        [SerializeField] private Vector3 driverSeatOffset = new Vector3(-0.35f, 1.25f, -0.3f);

        [Header("AR Pass-through Settings")]
        [Tooltip("Make camera background solid black (0,0,0,0) for HoloLens 2 transparent display.")]
        [SerializeField] private bool enableARPassthroughOnUWP = true;

        [Tooltip("Hide the solid roof/cabin cube on HoloLens so it doesn't obstruct vision.")]
        [SerializeField] private bool hideCabinOnHoloLens = true;

        [Header("Editor View Toggle")]
        [Tooltip("Key to toggle between Cockpit and Chase view in Unity Editor.")]
        [SerializeField] private KeyCode toggleViewKey = KeyCode.C;

        private bool isCockpitViewInEditor = false;
        private Transform previousParent;
        private Vector3 previousLocalPos;
        private Quaternion previousLocalRot;

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
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (UnityEngine.Input.GetKeyDown(toggleViewKey))
            {
                ToggleEditorCockpitView();
            }
#endif
        }

        private void ToggleEditorCockpitView()
        {
            Camera cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            var followCam = cam.GetComponent<SimpleFollowCamera>();
            isCockpitViewInEditor = !isCockpitViewInEditor;

            if (isCockpitViewInEditor)
            {
                var vehicle = FindObjectOfType<VehicleController>();
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
                    Debug.Log("[XRRigSetup] Switched to Cockpit Driver Seat View [C]");
                }
            }
            else
            {
                cam.transform.SetParent(null);
                if (followCam != null) followCam.enabled = true;
                Debug.Log("[XRRigSetup] Switched to Chase Follow Camera View [C]");
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
                cam.nearClipPlane = 0.1f;
            }

            // Find VehicleController in scene
            var vehicle = FindObjectOfType<VehicleController>();
            if (vehicle != null)
            {
                // Hide solid cabin cube so driver has unobstructed view
                if (hideCabinOnHoloLens)
                {
                    Transform cabin = vehicle.transform.Find("Body_Cabin");
                    if (cabin != null)
                    {
                        var renderer = cabin.GetComponent<MeshRenderer>();
                        if (renderer != null) renderer.enabled = false;
                    }
                }

                // Create a driver seat anchor on the vehicle
                Transform seatAnchor = vehicle.transform.Find("DriverSeatAnchor");
                if (seatAnchor == null)
                {
                    GameObject anchorObj = new GameObject("DriverSeatAnchor");
                    anchorObj.transform.SetParent(vehicle.transform, false);
                    anchorObj.transform.localPosition = driverSeatOffset;
                    anchorObj.transform.localRotation = Quaternion.identity;
                    seatAnchor = anchorObj.transform;
                }

                // Parent the Camera Rig to the seat anchor so driver moves with the car
                if (cam != null)
                {
                    // Disable SimpleFollowCamera on HoloLens so it does not override OpenXR head tracking
                    var followCam = cam.GetComponent<SimpleFollowCamera>();
                    if (followCam != null)
                    {
                        followCam.enabled = false;
                    }

                    cam.transform.SetParent(seatAnchor, false);
                    cam.transform.localPosition = Vector3.zero;
                    cam.transform.localRotation = Quaternion.identity;
                }

                Debug.Log("[XRRigSetup] HoloLens 2 camera anchored to vehicle driver seat.");
            }
        }
    }
}
