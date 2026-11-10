// AutoSimBuildTools.cs — Editor automated build helpers for HoloLens 2 UWP ARM64.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AutoSim.EditorTools
{
    /// <summary>
    /// Automated helper utilities to configure and generate the HoloLens 2 UWP ARM64 Visual Studio project.
    /// </summary>
    public static class AutoSimBuildTools
    {
        private const string BUILD_OUTPUT_DIRECTORY = "Builds/HoloLens2";
        private const string SCENE_PATH = "Assets/Scenes/DrivingSimulator.unity";

        /// <summary>
        /// Configures project settings required for HoloLens 2 UWP deployment.
        /// </summary>
        [MenuItem("AutoSim/HoloLens 2/1. Configure Settings for HoloLens 2", priority = 100)]
        public static void ConfigureForHoloLens()
        {
            // 1. Scripting Backend IL2CPP
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.WSA, ScriptingImplementation.IL2CPP);

            // 2. Target HoloLens and ARM64 architecture
            EditorUserBuildSettings.wsaArchitecture = "ARM64";
            EditorUserBuildSettings.wsaUWPBuildType = WSAUWPBuildType.D3D;

            // 3. Capabilities for HoloLens 2 networking and eye gaze
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.InternetClient, true);
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.InternetClientServer, true);
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.PrivateNetworkClientServer, true);
            PlayerSettings.WSA.SetCapability(PlayerSettings.WSACapability.GazeInput, true);

            AssetDatabase.SaveAssets();

            Debug.Log("[AutoSimBuildTools] HoloLens 2 UWP settings configured successfully: ARM64, D3D, IL2CPP, Capabilities enabled.");
            EditorUtility.DisplayDialog("AutoSim - HoloLens 2 Setup",
                "HoloLens 2 UWP build settings configured:\n\n" +
                "• Architecture: ARM64\n" +
                "• Target: HoloLens\n" +
                "• Scripting: IL2CPP\n" +
                "• Build Type: D3D\n" +
                "• Capabilities: InternetClient, PrivateNetworkClientServer, GazeInput\n\n" +
                "You can now switch platform (if not already on UWP) and generate the build.",
                "OK");
        }

        /// <summary>
        /// Generates the complete Visual Studio solution for HoloLens 2 in Builds/HoloLens2.
        /// </summary>
        [MenuItem("AutoSim/HoloLens 2/2. Generate HoloLens 2 Build (Visual Studio Solution)", priority = 101)]
        public static void GenerateHoloLensBuild()
        {
            ConfigureForHoloLens();

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string buildPath = Path.Combine(projectRoot, BUILD_OUTPUT_DIRECTORY);

            if (!Directory.Exists(buildPath))
            {
                Directory.CreateDirectory(buildPath);
            }

            Debug.Log($"[AutoSimBuildTools] Starting HoloLens 2 build to: {buildPath}...");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { SCENE_PATH },
                locationPathName = buildPath,
                target = BuildTarget.WSAPlayer,
                targetGroup = BuildTargetGroup.WSA,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[AutoSimBuildTools] Build succeeded! Size: {summary.totalSize} bytes, Time: {summary.totalTime.TotalSeconds:F1}s.");

                // Reveal folder in Windows Explorer
                EditorUtility.RevealInFinder(Path.Combine(buildPath, "AutoSim.sln"));

                EditorUtility.DisplayDialog("AutoSim - Build Succeeded!",
                    $"Visual Studio solution generated successfully in:\n{buildPath}\n\n" +
                    "Next Steps in Visual Studio:\n" +
                    "1. Open AutoSim.sln in Visual Studio 2022\n" +
                    "2. Select 'Release' and 'ARM64'\n" +
                    "3. Select 'Device' (USB) or 'Remote Machine' (Wi-Fi)\n" +
                    "4. Click Build > Deploy Solution",
                    "Open Folder");
            }
            else
            {
                Debug.LogError($"[AutoSimBuildTools] Build failed with result: {summary.result}. Check Console for details.");
            }
        }
    }
}
