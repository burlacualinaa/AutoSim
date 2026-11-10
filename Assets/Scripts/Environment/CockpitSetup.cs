// CockpitSetup.cs — Creates a minimal virtual car cockpit for HoloLens 2 AR.
// Provides dashboard, steering wheel, side panels, and rearview mirror placeholder.
using UnityEngine;

namespace AutoSim.Environment
{
    /// <summary>
    /// Generates a minimal virtual cockpit around the driver's head position.
    /// Designed for HoloLens 2 AR — uses semi-transparent materials where appropriate
    /// so the real world remains visible through the virtual car body.
    /// </summary>
    [AddComponentMenu("AutoSim/Environment/Cockpit Setup")]
    public class CockpitSetup : MonoBehaviour
    {
        // ──────────────────────────── Inspector Parameters ────────────────────────────

        [Header("Driver Position")]
        [Tooltip("Offset of the driver's seated position relative to this transform. " +
                 "Cockpit elements are placed around this point.")]
        [SerializeField] private Vector3 driverSeatOffset = new Vector3(0f, 0.5f, 0f);

        [Header("Dashboard")]
        [Tooltip("Width of the dashboard (metres).")]
        [SerializeField] private float dashboardWidth = 0.8f;

        [Tooltip("Depth of the dashboard (metres).")]
        [SerializeField] private float dashboardDepth = 0.3f;

        [Tooltip("Height/thickness of the dashboard (metres).")]
        [SerializeField] private float dashboardHeight = 0.1f;

        [Tooltip("Forward offset of dashboard from driver seat (metres).")]
        [SerializeField] private float dashboardForwardOffset = 0.45f;

        [Tooltip("Vertical offset of dashboard below driver eye level (metres).")]
        [SerializeField] private float dashboardDownOffset = 0.35f;

        [Header("Steering Wheel")]
        [Tooltip("Outer radius of the steering wheel ring (metres).")]
        [SerializeField] private float wheelRadius = 0.17f;

        [Tooltip("Thickness (tube radius) of the steering wheel rim (metres).")]
        [SerializeField] private float wheelTubeRadius = 0.015f;

        [Tooltip("Number of cylinder segments used to approximate the wheel ring.")]
        [SerializeField] private int wheelSegments = 24;

        [Tooltip("Tilt angle of the steering wheel towards the driver (degrees).")]
        [SerializeField] private float wheelTiltAngle = 25f;

        [Header("Side Panels")]
        [Tooltip("Lateral offset of door panels from centre (metres).")]
        [SerializeField] private float doorPanelOffset = 0.55f;

        [Tooltip("Height of door panels (metres).")]
        [SerializeField] private float doorPanelHeight = 0.35f;

        [Tooltip("Length of door panels along the forward axis (metres).")]
        [SerializeField] private float doorPanelLength = 0.6f;

        [Tooltip("Thickness of door panels (metres).")]
        [SerializeField] private float doorPanelThickness = 0.03f;

        [Header("Rearview Mirror")]
        [Tooltip("Width of the rearview mirror (metres).")]
        [SerializeField] private float mirrorWidth = 0.25f;

        [Tooltip("Height of the rearview mirror (metres).")]
        [SerializeField] private float mirrorHeight = 0.06f;

        // ──────────────────────────── Internals ───────────────────────────────────────

        private Transform _cockpitContainer;

        // ──────────────────────────── Public API ──────────────────────────────────────

        /// <summary>
        /// Generates all cockpit elements. Safe to call multiple times — clears previous.
        /// </summary>
        public void GenerateCockpit()
        {
            ClearCockpit();

            _cockpitContainer = new GameObject("Cockpit").transform;
            _cockpitContainer.SetParent(transform, false);
            _cockpitContainer.localPosition = driverSeatOffset;

            CreateDashboard();
            CreateSteeringWheel();
            CreateDoorPanels();
            CreateRearviewMirror();

            Debug.Log("[CockpitSetup] Cockpit generated.");
        }

        /// <summary>Destroys all previously generated cockpit objects.</summary>
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
        }

        // ──────────────────────────── Dashboard ───────────────────────────────────────

        private void CreateDashboard()
        {
            GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dash.name = "Dashboard";
            dash.transform.SetParent(_cockpitContainer, false);
            dash.transform.localPosition = new Vector3(0f, -dashboardDownOffset, dashboardForwardOffset);
            dash.transform.localScale = new Vector3(dashboardWidth, dashboardHeight, dashboardDepth);

            ApplyMaterial(dash, new Color(0.15f, 0.15f, 0.15f), opacity: 0.85f); // dark gray, slightly transparent
        }

        // ──────────────────────────── Steering Wheel ──────────────────────────────────

        private void CreateSteeringWheel()
        {
            // Create a container for the wheel ring, tilted towards the driver
            Transform wheelPivot = new GameObject("SteeringWheel_Pivot").transform;
            wheelPivot.SetParent(_cockpitContainer, false);
            wheelPivot.localPosition = new Vector3(0f,
                -dashboardDownOffset + dashboardHeight / 2f + 0.08f,
                dashboardForwardOffset - dashboardDepth / 2f - 0.02f);
            wheelPivot.localRotation = Quaternion.Euler(wheelTiltAngle, 0f, 0f);

            // Approximate torus with small cylinders placed in a circle
            for (int i = 0; i < wheelSegments; i++)
            {
                float angle = (360f / wheelSegments) * i;
                float nextAngle = (360f / wheelSegments) * (i + 1);

                float rad = angle * Mathf.Deg2Rad;
                float nextRad = nextAngle * Mathf.Deg2Rad;

                Vector3 p0 = new Vector3(Mathf.Cos(rad) * wheelRadius, Mathf.Sin(rad) * wheelRadius, 0f);
                Vector3 p1 = new Vector3(Mathf.Cos(nextRad) * wheelRadius, Mathf.Sin(nextRad) * wheelRadius, 0f);

                Vector3 midpoint = (p0 + p1) / 2f;
                float segLength = Vector3.Distance(p0, p1);

                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                seg.name = $"WheelSeg_{i}";
                seg.transform.SetParent(wheelPivot, false);
                seg.transform.localPosition = midpoint;

                // Scale: X and Z are tube diameter, Y is half the segment length (cylinder height = 2*scaleY)
                seg.transform.localScale = new Vector3(wheelTubeRadius * 2f, segLength / 2f, wheelTubeRadius * 2f);

                // Rotate cylinder so its length axis points from p0 to p1
                Vector3 dir = (p1 - p0).normalized;
                seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);

                ApplyMaterial(seg, new Color(0.08f, 0.08f, 0.08f), opacity: 1f); // black rubber
            }
        }

        // ──────────────────────────── Door Panels ─────────────────────────────────────

        private void CreateDoorPanels()
        {
            // Left door panel
            CreateDoorPanel("DoorPanel_Left", -doorPanelOffset);
            // Right door panel
            CreateDoorPanel("DoorPanel_Right", doorPanelOffset);
        }

        private void CreateDoorPanel(string name, float xOffset)
        {
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = name;
            panel.transform.SetParent(_cockpitContainer, false);
            panel.transform.localPosition = new Vector3(xOffset, -dashboardDownOffset, 0.1f);
            panel.transform.localScale = new Vector3(doorPanelThickness, doorPanelHeight, doorPanelLength);

            ApplyMaterial(panel, new Color(0.12f, 0.12f, 0.14f), opacity: 0.7f); // semi-transparent dark
        }

        // ──────────────────────────── Rearview Mirror ─────────────────────────────────

        private void CreateRearviewMirror()
        {
            GameObject mirror = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mirror.name = "RearviewMirror";
            mirror.transform.SetParent(_cockpitContainer, false);

            // Position above and slightly forward of driver head
            mirror.transform.localPosition = new Vector3(0f, 0.15f, 0.3f);
            mirror.transform.localRotation = Quaternion.Euler(15f, 0f, 0f); // angled downward slightly
            mirror.transform.localScale = new Vector3(mirrorWidth, mirrorHeight, 1f);

            // Mirror-like reflective surface (approximated with high metallic)
            Material mirrorMat = new Material(Shader.Find("Standard"));
            mirrorMat.color = new Color(0.7f, 0.75f, 0.8f, 0.9f);
            mirrorMat.SetFloat("_Metallic", 0.9f);
            mirrorMat.SetFloat("_Glossiness", 0.95f);
            SetTransparentMode(mirrorMat);
            mirror.GetComponent<MeshRenderer>().material = mirrorMat;
        }

        // ──────────────────────────── Material Helpers ─────────────────────────────────

        /// <summary>
        /// Applies a simple Standard shader material to a GameObject.
        /// If opacity &lt; 1, enables transparent rendering mode.
        /// </summary>
        private static void ApplyMaterial(GameObject go, Color color, float opacity = 1f)
        {
            Material mat = new Material(Shader.Find("Standard"));
            color.a = opacity;
            mat.color = color;
            mat.SetFloat("_Glossiness", 0.1f);
            mat.SetFloat("_Metallic", 0f);

            if (opacity < 1f)
            {
                SetTransparentMode(mat);
            }

            go.GetComponent<MeshRenderer>().material = mat;
        }

        /// <summary>Switches a Standard shader material to Transparent rendering mode.</summary>
        private static void SetTransparentMode(Material mat)
        {
            mat.SetFloat("_Mode", 3f); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }
    }
}
