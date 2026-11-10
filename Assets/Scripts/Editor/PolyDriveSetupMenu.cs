// PolyDriveSetupMenu.cs — One-click Editor tool to activate PolyDrive Bavaria Sport as the main vehicle.
#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using AutoSim.Vehicle;
using AutoSim.XR;

namespace AutoSim.EditorTools
{
    public static class PolyDriveSetupMenu
    {
        [MenuItem("AutoSim/Activate PolyDrive Bavaria Sport", false, 10)]
        public static void ActivatePolyDriveCar()
        {
            // 1. Search for existing Bavaria Sport in the scene
            GameObject car = GameObject.Find("Bavaria_Sport-3");
            if (car == null) car = GameObject.Find("Bavaria_Sport");

            if (car == null)
            {
                // Search by prefix
                var allRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var r in allRoots)
                {
                    if (r.name.ToLowerInvariant().Contains("bavaria"))
                    {
                        car = r;
                        break;
                    }
                }
            }

            if (car == null)
            {
                // Load from Prefabs
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolyDrive - FREE/Prefabs/Bavaria_Sport-3.prefab");
                if (prefab != null)
                {
                    car = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    car.name = "Bavaria_Sport-3";
                    car.transform.position = new Vector3(0f, 0.4f, 10f);
                    car.transform.rotation = Quaternion.identity;
                    Undo.RegisterCreatedObjectUndo(car, "Instantiate Bavaria Sport");
                    Debug.Log("[PolyDriveSetupMenu] Instantiated Bavaria_Sport-3 from Prefabs.");
                }
            }

            if (car == null)
            {
                EditorUtility.DisplayDialog("AutoSim", "Could not find Bavaria_Sport in scene or prefabs!", "OK");
                return;
            }

            Undo.RecordObject(car, "Configure PolyDrive Car");

            // Disable old box Vehicle
            GameObject oldVehicle = GameObject.Find("Vehicle");
            if (oldVehicle != null && oldVehicle != car)
            {
                Undo.RecordObject(oldVehicle, "Disable Old Vehicle");
                oldVehicle.SetActive(false);
            }

            // Attach & Run PolyDriveCarSetup
            var setup = car.GetComponent<PolyDriveCarSetup>();
            if (setup == null)
            {
                setup = car.AddComponent<PolyDriveCarSetup>();
            }
            setup.SetupPolyDriveCar();

            // Setup Camera
            Camera cam = Camera.main;
            if (cam != null)
            {
                var xr = cam.GetComponent<XRRigSetup>();
                if (xr != null)
                {
                    Undo.RecordObject(xr, "Configure XR Rig");
                    // Ensure driver seat offset matches Bavaria Sport front driver seat
                    var seatOffsetProp = new SerializedObject(xr).FindProperty("driverSeatOffset");
                    if (seatOffsetProp != null)
                    {
                        seatOffsetProp.vector3Value = new Vector3(-0.364f, 1.12f, 0.05f);
                        seatOffsetProp.serializedObject.ApplyModifiedProperties();
                    }
                    xr.SetEditorCockpitView(true);
                }

                var follow = cam.GetComponent<SimpleFollowCamera>();
                if (follow != null)
                {
                    Undo.RecordObject(follow, "Set Follow Camera Target");
                    follow.Target = car.transform;
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(car.scene);
            Selection.activeGameObject = car;

            EditorUtility.DisplayDialog("AutoSim", 
                "PolyDrive Bavaria Sport is now configured and active!\n\n" +
                "- Old placeholder Vehicle has been disabled.\n" +
                "- 4 WheelColliders & LOD wheel sync are configured.\n" +
                "- Interior SteeringWheel is connected to Logitech G920.\n" +
                "- Cockpit Driver Camera & Chase Camera are ready.\n\n" +
                "Press Play to drive!", "Great!");
        }
    }
}

#endif
