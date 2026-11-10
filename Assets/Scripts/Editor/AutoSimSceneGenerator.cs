// AutoSimSceneGenerator.cs — Editor menu tool to auto-generate the test driving scene.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using AutoSim.Vehicle;
using AutoSim.Input;
using AutoSim.Environment;
using AutoSim.UI;
using AutoSim.Networking;
using AutoSim.XR;

namespace AutoSim.Editor
{
    /// <summary>
    /// Editor tool that creates or populates the driving simulator scene with a
    /// procedural straight road, drivable vehicle (using Unity primitives and WheelColliders),
    /// keyboard input, speedometer HUD, and follow camera.
    /// </summary>
    public static class AutoSimSceneGenerator
    {
        private const string SCENE_PATH = "Assets/Scenes/DrivingSimulator.unity";

        /// <summary>
        /// Scans the currently open scene, attaches any missing XR, networking, and input components,
        /// and fixes all WheelCollider references on the Vehicle.
        /// </summary>
        [MenuItem("AutoSim/Fix and Wire Current Scene", false, 1)]
        public static void FixAndWireCurrentScene()
        {
            var scene = EditorSceneManager.GetActiveScene();

            // 1. Find VehicleController
            var vc = Object.FindObjectOfType<VehicleController>();
            if (vc != null)
            {
                vc.EnsureWheelColliders();

                // Ensure UDP receiver, network input provider, and network manager
                if (vc.GetComponent<UDPInputReceiver>() == null)
                    vc.gameObject.AddComponent<UDPInputReceiver>();

                if (vc.GetComponent<NetworkInputProvider>() == null)
                    vc.gameObject.AddComponent<NetworkInputProvider>();

                if (vc.GetComponent<NetworkManager>() == null)
                    vc.gameObject.AddComponent<NetworkManager>();

                // Ensure keyboard fallback
                var keyboard = vc.GetComponent<KeyboardInputProvider>();
                if (keyboard == null)
                    keyboard = vc.gameObject.AddComponent<KeyboardInputProvider>();

                var g920 = vc.GetComponent<LogitechG920InputProvider>();
                if (g920 != null)
                {
                    var serializedG920 = new SerializedObject(g920);
                    var kbProp = serializedG920.FindProperty("keyboardFallback");
                    if (kbProp != null && kbProp.objectReferenceValue == null)
                    {
                        kbProp.objectReferenceValue = keyboard;
                        serializedG920.ApplyModifiedProperties();
                    }
                }

                EditorUtility.SetDirty(vc.gameObject);
                Debug.Log("[AutoSimSceneGenerator] Vehicle components and WheelColliders verified.");
            }

            // 2. Find Camera
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindObjectOfType<Camera>();
            if (cam != null)
            {
                if (cam.GetComponent<XRRigSetup>() == null)
                {
                    cam.gameObject.AddComponent<XRRigSetup>();
                    Debug.Log("[AutoSimSceneGenerator] Added XRRigSetup to Main Camera.");
                }

                if (vc != null)
                {
                    var followCam = cam.GetComponent<SimpleFollowCamera>();
                    if (followCam != null && followCam.Target == null)
                    {
                        followCam.Target = vc.transform;
                    }
                }

                EditorUtility.SetDirty(cam.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[AutoSimSceneGenerator] Scene fixed and saved successfully!");
            EditorUtility.DisplayDialog("AutoSim", "Current scene validated and saved!\n\n• WheelColliders verified\n• XRRigSetup added to Camera\n• UDPInputReceiver and NetworkManager added to Vehicle", "OK");
        }

        [MenuItem("AutoSim/Generate Driving Scene", false, 10)]
        public static void GenerateDrivingScene()
        {
            // Open or create the scene
            UnityEngine.SceneManagement.Scene scene;
            if (System.IO.File.Exists(SCENE_PATH))
            {
                scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            // Clear existing root objects
            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int i = rootObjects.Length - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(rootObjects[i]);
            }

            // 1. Create Directional Light
            CreateDirectionalLight();

            // 2. Create Environment (Straight Road)
            GameObject envObj = CreateEnvironment();

            // 3. Create Drivable Vehicle
            GameObject vehicleObj = CreateVehicle(new Vector3(0f, 0.5f, 10f));

            // 4. Create Follow Camera
            CreateFollowCamera(vehicleObj.transform);

            // Mark dirty and save
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, SCENE_PATH);

            Debug.Log("[AutoSimSceneGenerator] Driving simulator scene generated and saved at: " + SCENE_PATH);
            EditorUtility.DisplayDialog("AutoSim", "Driving scene generated successfully!\n\nPress PLAY and drive with W, A, S, D / Arrow keys.", "OK");
        }

        private static void CreateDirectionalLight()
        {
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private static GameObject CreateEnvironment()
        {
            GameObject envObj = new GameObject("Environment");
            ProceduralRoadGenerator roadGen = envObj.AddComponent<ProceduralRoadGenerator>();
            roadGen.GenerateRoad();
            return envObj;
        }

        private static GameObject CreateVehicle(Vector3 spawnPosition)
        {
            GameObject vehicle = new GameObject("Vehicle");
            vehicle.transform.position = spawnPosition;
            vehicle.transform.rotation = Quaternion.identity;

            // Rigidbody
            Rigidbody rb = vehicle.AddComponent<Rigidbody>();
            rb.mass = AutoSimConstants.VEHICLE_MASS;
            rb.drag = 0.05f;
            rb.angularDrag = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Chassis Box Collider (elevated so it does not interfere with WheelColliders)
            BoxCollider chassisCollider = vehicle.AddComponent<BoxCollider>();
            chassisCollider.center = new Vector3(0f, 0.65f, 0f);
            chassisCollider.size = new Vector3(1.8f, 0.6f, 4.2f);

            // Visual Car Body
            CreateVisualBody(vehicle.transform);

            // Wheel Colliders
            // Standard vehicle geometry: track width ~1.8m (±0.9m), wheelbase ~2.7m (±1.35m)
            WheelCollider fl = CreateWheelCollider(vehicle.transform, "WheelCollider_FL", new Vector3(-0.9f, 0.35f, 1.35f));
            WheelCollider fr = CreateWheelCollider(vehicle.transform, "WheelCollider_FR", new Vector3(0.9f, 0.35f, 1.35f));
            WheelCollider rl = CreateWheelCollider(vehicle.transform, "WheelCollider_RL", new Vector3(-0.9f, 0.35f, -1.35f));
            WheelCollider rr = CreateWheelCollider(vehicle.transform, "WheelCollider_RR", new Vector3(0.9f, 0.35f, -1.35f));

            // Visual Wheels
            CreateVisualWheel(vehicle.transform, "VisualWheel_FL", new Vector3(-0.9f, 0.35f, 1.35f), fl);
            CreateVisualWheel(vehicle.transform, "VisualWheel_FR", new Vector3(0.9f, 0.35f, 1.35f), fr);
            CreateVisualWheel(vehicle.transform, "VisualWheel_RL", new Vector3(-0.9f, 0.35f, -1.35f), rl);
            CreateVisualWheel(vehicle.transform, "VisualWheel_RR", new Vector3(0.9f, 0.35f, -1.35f), rr);

            // Vehicle Controller
            VehicleController vc = vehicle.AddComponent<VehicleController>();
            vc.SetWheelColliders(fl, fr, rl, rr);

            // Input Providers (Logitech G920 Primary with Keyboard Fallback)
            KeyboardInputProvider keyboard = vehicle.AddComponent<KeyboardInputProvider>();
            LogitechG920InputProvider g920 = vehicle.AddComponent<LogitechG920InputProvider>();
            g920.KeyboardFallback = keyboard;
            vc.ActiveInputProvider = g920;
            vc.InputProviderSource = g920;

            // Network synchronization for HoloLens 2
            vehicle.AddComponent<UDPInputReceiver>();
            vehicle.AddComponent<NetworkInputProvider>();
            vehicle.AddComponent<NetworkManager>();

            // Speedometer & UI
            vehicle.AddComponent<Speedometer>();
            vehicle.AddComponent<SpeedometerUI>();

            // Cockpit & Interior Simulation
            vehicle.AddComponent<CockpitSetup>();

            return vehicle;
        }

        private static void CreateVisualBody(Transform vehicleTransform)
        {
            Material bodyMat = new Material(Shader.Find("Standard"))
            {
                name = "CarBody_Mat",
                color = new Color(0.12f, 0.35f, 0.75f) // Electric blue
            };
            bodyMat.SetFloat("_Metallic", 0.6f);
            bodyMat.SetFloat("_Glossiness", 0.7f);

            Material cabinMat = new Material(Shader.Find("Standard"))
            {
                name = "CarCabin_Mat",
                color = new Color(0.15f, 0.15f, 0.18f) // Tinted glass / dark cabin
            };
            cabinMat.SetFloat("_Glossiness", 0.9f);

            // Lower body box
            GameObject lowerBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lowerBody.name = "Body_Lower";
            lowerBody.transform.SetParent(vehicleTransform, false);
            lowerBody.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            lowerBody.transform.localScale = new Vector3(1.8f, 0.6f, 4.2f);
            Object.DestroyImmediate(lowerBody.GetComponent<Collider>());
            lowerBody.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            // Cabin box
            GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.name = "Body_Cabin";
            cabin.transform.SetParent(vehicleTransform, false);
            cabin.transform.localPosition = new Vector3(0f, 1.15f, -0.3f);
            cabin.transform.localScale = new Vector3(1.5f, 0.5f, 2.2f);
            Object.DestroyImmediate(cabin.GetComponent<Collider>());
            cabin.GetComponent<MeshRenderer>().sharedMaterial = cabinMat;
        }

        private static WheelCollider CreateWheelCollider(Transform vehicleTransform, string name, Vector3 localPos)
        {
            GameObject wheelObj = new GameObject(name);
            wheelObj.transform.SetParent(vehicleTransform, false);
            wheelObj.transform.localPosition = localPos;

            WheelCollider wc = wheelObj.AddComponent<WheelCollider>();
            wc.radius = 0.35f;
            wc.wheelDampingRate = 0.25f;
            wc.suspensionDistance = 0.2f;

            // Suspension spring setup
            JointSpring spring = new JointSpring
            {
                spring = 35000f,
                damper = 4500f,
                targetPosition = 0.5f
            };
            wc.suspensionSpring = spring;

            // Friction curves for realistic grip
            WheelFrictionCurve forwardCurve = wc.forwardFriction;
            forwardCurve.extremumSlip = 0.2f;
            forwardCurve.extremumValue = 1f;
            forwardCurve.asymptoteSlip = 0.8f;
            forwardCurve.asymptoteValue = 0.65f;
            forwardCurve.stiffness = 1.5f;
            wc.forwardFriction = forwardCurve;

            WheelFrictionCurve sidewaysCurve = wc.sidewaysFriction;
            sidewaysCurve.extremumSlip = 0.2f;
            sidewaysCurve.extremumValue = 1f;
            sidewaysCurve.asymptoteSlip = 0.5f;
            sidewaysCurve.asymptoteValue = 0.75f;
            sidewaysCurve.stiffness = 1.5f;
            wc.sidewaysFriction = sidewaysCurve;

            return wc;
        }

        private static void CreateVisualWheel(Transform vehicleTransform, string name, Vector3 localPos, WheelCollider targetWc)
        {
            GameObject visualWheel = new GameObject(name);
            visualWheel.transform.SetParent(vehicleTransform, false);
            visualWheel.transform.localPosition = localPos;

            // Cylinder primitive rotated 90 degrees around Z to align with wheel axle
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = "TireMesh";
            cylinder.transform.SetParent(visualWheel.transform, false);
            cylinder.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            cylinder.transform.localScale = new Vector3(0.7f, 0.15f, 0.7f); // diameter 0.7m (radius 0.35), width 0.3m
            Object.DestroyImmediate(cylinder.GetComponent<Collider>());

            Material tireMat = new Material(Shader.Find("Standard"))
            {
                name = "Tire_Mat",
                color = new Color(0.12f, 0.12f, 0.12f) // Rubber dark gray
            };
            tireMat.SetFloat("_Glossiness", 0.1f);
            cylinder.GetComponent<MeshRenderer>().sharedMaterial = tireMat;

            // Add WheelVisualSync
            WheelVisualSync sync = visualWheel.AddComponent<WheelVisualSync>();
            sync.TargetWheel = targetWc;
        }

        private static void CreateFollowCamera(Transform vehicleTransform)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;

            camObj.AddComponent<AudioListener>();

            SimpleFollowCamera follow = camObj.AddComponent<SimpleFollowCamera>();
            follow.Target = vehicleTransform;
            camObj.transform.position = vehicleTransform.position + new Vector3(0f, 2.5f, -6f);
            camObj.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

            camObj.AddComponent<XRRigSetup>();
        }
    }
}
