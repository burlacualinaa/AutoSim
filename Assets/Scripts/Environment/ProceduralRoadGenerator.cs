// ProceduralRoadGenerator.cs — Generates an extended 2.5 km urban boulevard.
// Includes 4-lane roadway, raised sidewalks with 3D physical curbs (borduri),
// speed bumps (limitatoare de viteză), pedestrian zebra crossings, street lamps, and trees.
using System.Collections.Generic;
using UnityEngine;

namespace AutoSim.Environment
{
    /// <summary>
    /// Generates a grand 2.5 km urban boulevard featuring:
    /// - 4-lane wide asphalt roadway with lane dividers and shoulder markings.
    /// - Raised sidewalks (trotuare) with physical curb colliders (borduri) that trigger G920 Force Feedback.
    /// - Physical speed bumps (limitatoare de viteză) for authentic haptic suspension shocks.
    /// - High-visibility zebra pedestrian crossings.
    /// - Architectural streetlamp poles and boulevard tree alignments.
    /// </summary>
    [AddComponentMenu("AutoSim/Environment/Procedural Road Generator")]
    public class ProceduralRoadGenerator : MonoBehaviour
    {
        // ──────────────────────────── Inspector Parameters ────────────────────────────

        [Header("Road Dimensions (2.5 km Boulevard)")]
        [Tooltip("Total length of the boulevard along the Z axis (metres).")]
        [SerializeField] private float roadLength = 2500f;

        [Tooltip("Number of lanes per direction (2 = 4 lanes total).")]
        [SerializeField] private int lanesPerDirection = 2;

        [Tooltip("Width of each single driving lane (metres).")]
        [SerializeField] private float laneWidth = 3.6f;

        [Tooltip("Width of the asphalt shoulder on each side (metres).")]
        [SerializeField] private float shoulderWidth = 0.6f;

        [Header("Sidewalks & Curbs (Trotuare & Borduri)")]
        [Tooltip("Width of the pedestrian sidewalk on each side (metres).")]
        [SerializeField] private float sidewalkWidth = 3.5f;

        [Tooltip("Height of the curb stone above road level (metres).")]
        [SerializeField] private float curbHeight = 0.18f;

        [Tooltip("Width of the physical curb border (metres).")]
        [SerializeField] private float curbWidth = 0.25f;

        [Header("Speed Bumps (Limitatoare de Viteză / Denivelări)")]
        [Tooltip("Positions along Z where physical speed bumps are placed.")]
        [SerializeField] private float[] speedBumpPositions = new float[] { 320f, 750f, 1300f, 1850f, 2300f };

        [Tooltip("Height of the speed bump (metres).")]
        [SerializeField] private float speedBumpHeight = 0.085f;

        [Tooltip("Length of the speed bump along driving direction (metres).")]
        [SerializeField] private float speedBumpLength = 2.4f;

        [Header("Pedestrian Crossings (Treceri de Pietoni)")]
        [Tooltip("Positions along Z where zebra crossings are generated.")]
        [SerializeField] private float[] crosswalkPositions = new float[] { 220f, 650f, 1200f, 1750f, 2200f };

        [Header("Boulevard Furniture & Vegetation")]
        [Tooltip("Spacing between successive street light poles (metres).")]
        [SerializeField] private float lampPostSpacing = 48f;

        [Tooltip("Spacing between sidewalk trees (metres).")]
        [SerializeField] private float treeSpacing = 32f;

        [Header("Ground & Extent")]
        [Tooltip("Green ground plane extension beyond sidewalks on each side (metres).")]
        [SerializeField] private float groundExtent = 90f;

        [Tooltip("Automatically generate the road on Start if not present.")]
        [SerializeField] private bool generateOnStart = true;

        // ──────────────────────────── Calculated Properties ───────────────────────────

        /// <summary>Width of the asphalt roadway (all lanes + shoulders).</summary>
        public float AsphaltRoadWidth => (laneWidth * lanesPerDirection * 2f) + (shoulderWidth * 2f);

        /// <summary>Total road width including sidewalks.</summary>
        public float TotalRoadWidth => AsphaltRoadWidth + (sidewalkWidth * 2f);

        /// <summary>Total boulevard length.</summary>
        public float RoadLength => roadLength;

        // Container transform for easy cleanup
        private Transform _roadContainer;

        // ──────────────────────────── Unity Lifecycle ─────────────────────────────────

        private void Start()
        {
            if (generateOnStart && _roadContainer == null)
            {
                GenerateRoad();
            }
        }

        // ──────────────────────────── Generation API ──────────────────────────────────

        /// <summary>
        /// Generates the complete 2.5 km urban boulevard.
        /// </summary>
        [ContextMenu("Generate 2.5 km Urban Boulevard")]
        public void GenerateRoad()
        {
            ClearRoad();

            _roadContainer = new GameObject("UrbanBoulevard_2.5km").transform;
            _roadContainer.SetParent(transform, false);

            // 1. Surrounding Grass Ground
            CreateGroundPlanes();

            // 2. Main Asphalt Roadway
            CreateAsphaltSurface();

            // 3. Lane Dividers and Center Markings
            CreateRoadMarkings();

            // 4. Raised Sidewalks with 3D Curbs (Borduri Fizice)
            CreateSidewalksAndCurbs();

            // 5. Physical Speed Bumps (Limitatoare de viteză cu FFB shock)
            CreateSpeedBumps();

            // 6. Zebra Crosswalks
            CreatePedestrianCrossings();

            // 7. Streetlight Poles
            CreateStreetlamps();

            // 8. Sidewalk Trees
            CreateBoulevardTrees();

            Debug.Log($"[ProceduralRoadGenerator] Grand 2.5 km Urban Boulevard generated successfully! " +
                      $"Length: {roadLength}m | Asphalt Width: {AsphaltRoadWidth:F1}m (4 lanes) | Total Width: {TotalRoadWidth:F1}m");
        }

        /// <summary>Destroys all previously generated boulevard objects.</summary>
        [ContextMenu("Clear Road Geometry")]
        public void ClearRoad()
        {
            if (_roadContainer != null)
            {
                if (Application.isPlaying)
                    Destroy(_roadContainer.gameObject);
                else
                    DestroyImmediate(_roadContainer.gameObject);
                _roadContainer = null;
            }
        }

        // ──────────────────────────── Ground Planes ───────────────────────────────────

        private void CreateGroundPlanes()
        {
            float halfBoulevard = TotalRoadWidth / 2f;
            Material grassMat = CreateFlatMaterial("BoulevardGrass", new Color(0.22f, 0.48f, 0.18f));

            // Left grass plane
            CreateQuadObject("Ground_Left",
                new Vector3(-(halfBoulevard + groundExtent / 2f), -0.02f, roadLength / 2f),
                groundExtent, roadLength, grassMat, true);

            // Right grass plane
            CreateQuadObject("Ground_Right",
                new Vector3(halfBoulevard + groundExtent / 2f, -0.02f, roadLength / 2f),
                groundExtent, roadLength, grassMat, true);
        }

        // ──────────────────────────── Asphalt Surface ─────────────────────────────────

        private void CreateAsphaltSurface()
        {
            Material asphaltMat = CreateFlatMaterial("BoulevardAsphalt", new Color(0.14f, 0.14f, 0.16f), 0.15f);
            CreateQuadObject("AsphaltRoadway",
                new Vector3(0f, 0f, roadLength / 2f),
                AsphaltRoadWidth, roadLength, asphaltMat, true);
        }

        // ──────────────────────────── Road Markings ───────────────────────────────────

        private void CreateRoadMarkings()
        {
            Transform markContainer = new GameObject("RoadMarkings").transform;
            markContainer.SetParent(_roadContainer, false);

            Material whiteMarkMat = CreateFlatMaterial("MarkingWhite", new Color(0.95f, 0.95f, 0.95f));
            Material yellowLineMat = CreateFlatMaterial("MarkingYellow", new Color(1.0f, 0.82f, 0.12f));

            float markY = 0.012f;
            float halfAsphalt = AsphaltRoadWidth / 2f;

            // Double Yellow Center Line
            float yellowSpacing = 0.18f;
            CreateMarkingQuad("CenterYellow_L", new Vector3(-yellowSpacing, markY, roadLength / 2f), 0.14f, roadLength, yellowLineMat, markContainer);
            CreateMarkingQuad("CenterYellow_R", new Vector3(yellowSpacing, markY, roadLength / 2f), 0.14f, roadLength, yellowLineMat, markContainer);

            // Outer Solid White Edge Lines (along the inner edge of shoulders)
            float leftEdgeX = -halfAsphalt + shoulderWidth;
            float rightEdgeX = halfAsphalt - shoulderWidth;
            CreateMarkingQuad("EdgeLine_L", new Vector3(leftEdgeX, markY, roadLength / 2f), 0.18f, roadLength, whiteMarkMat, markContainer);
            CreateMarkingQuad("EdgeLine_R", new Vector3(rightEdgeX, markY, roadLength / 2f), 0.18f, roadLength, whiteMarkMat, markContainer);

            // Dashed White Lane Dividers for 4-lane road
            float laneSpan = laneWidth;
            float dashCycle = 9f; // 3m dash + 6m gap
            int dashCount = Mathf.FloorToInt(roadLength / dashCycle);

            for (int i = 0; i < dashCount; i++)
            {
                float z = (i * dashCycle) + 1.5f;

                // Left direction divider (between lane 1 and lane 2)
                CreateMarkingQuad($"LaneDash_L_{i}", new Vector3(-laneSpan, markY, z), 0.15f, 3.2f, whiteMarkMat, markContainer);

                // Right direction divider (between lane 1 and lane 2)
                CreateMarkingQuad($"LaneDash_R_{i}", new Vector3(laneSpan, markY, z), 0.15f, 3.2f, whiteMarkMat, markContainer);
            }
        }

        // ──────────────────────────── Raised Sidewalks & Curbs ────────────────────────

        private void CreateSidewalksAndCurbs()
        {
            Transform swContainer = new GameObject("SidewalksAndCurbs").transform;
            swContainer.SetParent(_roadContainer, false);

            Material sidewalkPavingMat = CreateFlatMaterial("SidewalkPaving", new Color(0.68f, 0.70f, 0.72f), 0.1f);
            Material curbGraniteMat = CreateFlatMaterial("CurbGranite", new Color(0.85f, 0.86f, 0.88f), 0.2f);

            float halfAsphalt = AsphaltRoadWidth / 2f;
            float swCenterX = halfAsphalt + (sidewalkWidth / 2f);

            // Left Raised Sidewalk (BoxCollider for walking/driving surface)
            CreateBoxSidewalk("Sidewalk_Left", new Vector3(-swCenterX, curbHeight / 2f, roadLength / 2f),
                new Vector3(sidewalkWidth, curbHeight, roadLength), sidewalkPavingMat, swContainer);

            // Right Raised Sidewalk
            CreateBoxSidewalk("Sidewalk_Right", new Vector3(swCenterX, curbHeight / 2f, roadLength / 2f),
                new Vector3(sidewalkWidth, curbHeight, roadLength), sidewalkPavingMat, swContainer);

            // 3D Physical Curbs (Borduri de piatră) along inner edge
            // Left Curb
            CreateBoxSidewalk("Curb_Left", new Vector3(-halfAsphalt - (curbWidth / 2f), (curbHeight + 0.02f) / 2f, roadLength / 2f),
                new Vector3(curbWidth, curbHeight + 0.02f, roadLength), curbGraniteMat, swContainer);

            // Right Curb
            CreateBoxSidewalk("Curb_Right", new Vector3(halfAsphalt + (curbWidth / 2f), (curbHeight + 0.02f) / 2f, roadLength / 2f),
                new Vector3(curbWidth, curbHeight + 0.02f, roadLength), curbGraniteMat, swContainer);
        }

        // ──────────────────────────── Speed Bumps (Denivelări FFB) ───────────────────

        private void CreateSpeedBumps()
        {
            if (speedBumpPositions == null || speedBumpPositions.Length == 0) return;

            Transform bumpContainer = new GameObject("SpeedBumps_FFB").transform;
            bumpContainer.SetParent(_roadContainer, false);

            Material bumpYellowMat = CreateFlatMaterial("SpeedBumpYellow", new Color(1.0f, 0.82f, 0.05f));
            Material bumpBlackMat = CreateFlatMaterial("SpeedBumpBlack", new Color(0.12f, 0.12f, 0.12f));

            float roadSpan = AsphaltRoadWidth - (shoulderWidth * 2f);

            for (int b = 0; b < speedBumpPositions.Length; b++)
            {
                float z = speedBumpPositions[b];
                if (z > roadLength - 20f) continue;

                GameObject bumpObj = new GameObject($"SpeedBump_Z{z:F0}");
                bumpObj.transform.SetParent(bumpContainer, false);
                bumpObj.transform.position = new Vector3(0f, speedBumpHeight / 2f, z);

                // Physical Ramp BoxCollider across the road
                BoxCollider col = bumpObj.AddComponent<BoxCollider>();
                col.size = new Vector3(roadSpan, speedBumpHeight, speedBumpLength);

                // Visual striped bump mesh
                int stripeCount = 12;
                float stripeW = roadSpan / stripeCount;
                float startX = -roadSpan / 2f + stripeW / 2f;

                for (int s = 0; s < stripeCount; s++)
                {
                    float x = startX + (s * stripeW);
                    Material mat = (s % 2 == 0) ? bumpYellowMat : bumpBlackMat;

                    GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    seg.name = $"Stripe_{s}";
                    seg.transform.SetParent(bumpObj.transform, false);
                    seg.transform.localPosition = new Vector3(x, 0f, 0f);
                    seg.transform.localScale = new Vector3(stripeW * 0.95f, speedBumpHeight, speedBumpLength);

                    // Destroy duplicate primitive collider (parent BoxCollider handles physics)
                    DestroyImmediate(seg.GetComponent<Collider>());
                    seg.GetComponent<MeshRenderer>().sharedMaterial = mat;
                }
            }
        }

        // ──────────────────────────── Zebra Pedestrian Crossings ──────────────────────

        private void CreatePedestrianCrossings()
        {
            if (crosswalkPositions == null || crosswalkPositions.Length == 0) return;

            Transform cwContainer = new GameObject("ZebraCrossings").transform;
            cwContainer.SetParent(_roadContainer, false);

            Material zebraMat = CreateFlatMaterial("ZebraWhite", Color.white);
            float span = AsphaltRoadWidth - (shoulderWidth * 2f);
            float stripeLength = 4.0f;
            float stripeWidth = 0.50f;
            float stripeGap = 0.50f;
            int stripeCount = Mathf.FloorToInt(span / (stripeWidth + stripeGap));

            for (int i = 0; i < crosswalkPositions.Length; i++)
            {
                float z = crosswalkPositions[i];
                if (z > roadLength - 30f) continue;

                GameObject zebraGroup = new GameObject($"ZebraCrossing_Z{z:F0}");
                zebraGroup.transform.SetParent(cwContainer, false);

                float startX = -((stripeCount * (stripeWidth + stripeGap)) / 2f) + (stripeWidth / 2f);
                for (int s = 0; s < stripeCount; s++)
                {
                    float x = startX + s * (stripeWidth + stripeGap);
                    CreateMarkingQuad($"Stripe_{s}", new Vector3(x, 0.016f, z), stripeWidth, stripeLength, zebraMat, zebraGroup.transform);
                }
            }
        }

        // ──────────────────────────── Streetlight Poles ───────────────────────────────

        private void CreateStreetlamps()
        {
            Transform lampContainer = new GameObject("StreetLamps").transform;
            lampContainer.SetParent(_roadContainer, false);

            Material poleMat = CreateFlatMaterial("LampPoleMetal", new Color(0.32f, 0.35f, 0.38f), 0.4f);
            Material lightHeadMat = CreateFlatMaterial("LampGlow", new Color(1.0f, 0.96f, 0.85f));

            float lampOffsetFromCurb = 1.2f;
            float halfAsphalt = AsphaltRoadWidth / 2f;
            float leftX = -(halfAsphalt + lampOffsetFromCurb);
            float rightX = halfAsphalt + lampOffsetFromCurb;

            int lampCount = Mathf.FloorToInt(roadLength / lampPostSpacing);

            for (int i = 0; i < lampCount; i++)
            {
                float z = 30f + (i * lampPostSpacing);
                if (z > roadLength - 20f) break;

                // Alternate sides or place on both sides
                CreateSingleLampPost($"Lamp_L_{i}", new Vector3(leftX, curbHeight, z), true, poleMat, lightHeadMat, lampContainer);
                CreateSingleLampPost($"Lamp_R_{i}", new Vector3(rightX, curbHeight, z), false, poleMat, lightHeadMat, lampContainer);
            }
        }

        private void CreateSingleLampPost(string name, Vector3 basePos, bool pointsRight, Material poleMat, Material glowMat, Transform parent)
        {
            GameObject lamp = new GameObject(name);
            lamp.transform.SetParent(parent, false);
            lamp.transform.position = basePos;

            // Vertical Pole
            float poleHeight = 7.0f;
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(lamp.transform, false);
            pole.transform.localPosition = new Vector3(0f, poleHeight / 2f, 0f);
            pole.transform.localScale = new Vector3(0.18f, poleHeight / 2f, 0.18f);
            pole.GetComponent<MeshRenderer>().sharedMaterial = poleMat;

            // Horizontal Overhang Arm toward road
            float armLen = 2.2f;
            float dir = pointsRight ? 1f : -1f;
            GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arm.name = "Arm";
            arm.transform.SetParent(lamp.transform, false);
            arm.transform.localPosition = new Vector3(dir * (armLen / 2f), poleHeight, 0f);
            arm.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            arm.transform.localScale = new Vector3(0.12f, armLen / 2f, 0.12f);
            arm.GetComponent<MeshRenderer>().sharedMaterial = poleMat;

            // Luminaire Light Head
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(lamp.transform, false);
            head.transform.localPosition = new Vector3(dir * armLen, poleHeight - 0.1f, 0f);
            head.transform.localScale = new Vector3(0.40f, 0.12f, 0.65f);
            head.GetComponent<MeshRenderer>().sharedMaterial = glowMat;
        }

        // ──────────────────────────── Boulevard Trees ─────────────────────────────────

        private void CreateBoulevardTrees()
        {
            Transform treeContainer = new GameObject("BoulevardTrees").transform;
            treeContainer.SetParent(_roadContainer, false);

            Material trunkMat = CreateFlatMaterial("TreeTrunk", new Color(0.35f, 0.24f, 0.15f));
            Material canopyMat = CreateFlatMaterial("TreeCanopy", new Color(0.18f, 0.42f, 0.16f));

            float treeOffsetFromCurb = 2.5f;
            float halfAsphalt = AsphaltRoadWidth / 2f;
            float leftX = -(halfAsphalt + treeOffsetFromCurb);
            float rightX = halfAsphalt + treeOffsetFromCurb;

            int treeCount = Mathf.FloorToInt(roadLength / treeSpacing);

            for (int i = 0; i < treeCount; i++)
            {
                float z = 20f + (i * treeSpacing);
                if (z > roadLength - 20f) break;

                CreateSingleTree($"Tree_L_{i}", new Vector3(leftX, curbHeight, z), trunkMat, canopyMat, treeContainer);
                CreateSingleTree($"Tree_R_{i}", new Vector3(rightX, curbHeight, z), trunkMat, canopyMat, treeContainer);
            }
        }

        private void CreateSingleTree(string name, Vector3 basePos, Material trunkMat, Material canopyMat, Transform parent)
        {
            GameObject tree = new GameObject(name);
            tree.transform.SetParent(parent, false);
            tree.transform.position = basePos;

            // Trunk
            float trunkH = 2.8f;
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, trunkH / 2f, 0f);
            trunk.transform.localScale = new Vector3(0.35f, trunkH / 2f, 0.35f);
            trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMat;

            // Canopy
            float canopyH = 3.6f;
            GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "Canopy";
            canopy.transform.SetParent(tree.transform, false);
            canopy.transform.localPosition = new Vector3(0f, trunkH + (canopyH * 0.4f), 0f);
            canopy.transform.localScale = new Vector3(3.2f, canopyH, 3.2f);
            canopy.GetComponent<MeshRenderer>().sharedMaterial = canopyMat;
        }

        // ──────────────────────────── Mesh & Box Helpers ──────────────────────────────

        private void CreateMarkingQuad(string name, Vector3 pos, float w, float l, Material mat, Transform parent)
        {
            GameObject quad = CreateQuadMesh(pos, w, l);
            quad.name = name;
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
            quad.transform.SetParent(parent, false);
        }

        private void CreateQuadObject(string name, Vector3 position, float width, float length, Material mat, bool addCollider)
        {
            GameObject quad = CreateQuadMesh(position, width, length);
            quad.name = name;
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (addCollider)
            {
                var col = quad.AddComponent<MeshCollider>();
                col.sharedMesh = quad.GetComponent<MeshFilter>().sharedMesh;
            }
            quad.transform.SetParent(_roadContainer, true);
        }

        private void CreateBoxSidewalk(string name, Vector3 center, Vector3 size, Material mat, Transform parent)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = center;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        public static GameObject CreateQuadMesh(Vector3 origin, float width, float length)
        {
            GameObject go = new GameObject("Quad");

            float hw = width / 2f;
            float hl = length / 2f;

            Vector3[] verts = new Vector3[]
            {
                new Vector3(-hw, 0f, -hl),
                new Vector3( hw, 0f, -hl),
                new Vector3( hw, 0f,  hl),
                new Vector3(-hw, 0f,  hl)
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };

            int[] tris = new int[] { 0, 2, 1, 0, 3, 2 };

            Vector3[] normals = new Vector3[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up
            };

            Mesh mesh = new Mesh
            {
                name = "QuadMesh",
                vertices = verts,
                uv = uvs,
                triangles = tris,
                normals = normals
            };

            go.AddComponent<MeshFilter>().mesh = mesh;
            go.AddComponent<MeshRenderer>();
            go.transform.position = origin;

            return go;
        }

        private static Material CreateFlatMaterial(string name, Color color, float glossiness = 0f)
        {
            Material mat = new Material(Shader.Find("Standard"))
            {
                name = name,
                color = color
            };
            mat.SetFloat("_Glossiness", glossiness);
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }
    }
}
