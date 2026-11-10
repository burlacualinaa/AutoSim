// OpenXRInputLayoutFixer.cs — Resolves the conflict between OpenXR's DPad interaction
// layout and Unity Input System's built-in DpadControl layout.
// Desktop / Editor only: excluded from HoloLens 2 UWP builds.
#if UNITY_EDITOR || !UNITY_WSA

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.Utilities;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AutoSim.Input
{
    /// <summary>
    /// Resolves the layout collision between OpenXR and Unity's Input System.
    /// 
    /// Root Cause:
    /// OpenXR package registers a Device layout named "DPad" (typeof(DPadInteraction.DPad)).
    /// Because layout names are case-insensitive, this replaces Unity's default Control layout "Dpad".
    /// When any device with a D-pad (such as the Logitech G920 racing wheel) connects, Unity's
    /// HID builder tries to instantiate child control "dpad" as layout "Dpad", sees it is registered
    /// as an InputDevice, and throws:
    /// InvalidOperationException: Cannot instantiate device layout 'Dpad' as child of '...'; devices must be added at root.
    /// 
    /// Solution:
    /// 1. Intercepts layout changes and checks if a Device layout named "DPad" was registered.
    /// 2. If conflicted, removes the conflicting "DPad" device layout.
    /// 3. Re-registers DpadControl as the control layout for "Dpad" and "DPad".
    /// 4. Re-registers OpenXR's device layout under "OpenXR_DPad" so OpenXR VR controllers remain functional.
    /// 5. Includes a strict re-entrancy lock and unhooks listeners during modification to prevent recursion.
    /// </summary>
    public static class OpenXRInputLayoutFixer
    {
        private static bool isInitialized = false;
        private static bool isApplying = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSubsystemRegistration()
        {
            ApplyLayoutFix();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            ApplyLayoutFix();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            ApplyLayoutFix();

            EditorApplication.playModeStateChanged += (state) =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.ExitingEditMode)
                {
                    ApplyLayoutFix();
                }
            };
        }

        [MenuItem("AutoSim/Fix OpenXR Input Layout Conflict", false, 100)]
        public static void MenuFixLayout()
        {
            ApplyLayoutFix(true);
            EditorUtility.DisplayDialog("AutoSim", "OpenXR DPad layout check completed.", "OK");
        }
#endif

        /// <summary>
        /// Inspects layout registrations and restores DpadControl if OpenXR registered a device layout as DPad.
        /// </summary>
        public static void ApplyLayoutFix(bool force = false)
        {
            if (isApplying)
                return;

            if (isInitialized && !force)
                return;

            try
            {
                // Hook listener safely
                InputSystem.onLayoutChange -= OnLayoutChange;
                InputSystem.onLayoutChange += OnLayoutChange;

                EnsureDpadControlLayout();
                isInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OpenXRInputLayoutFixer] Layout initialization note: {ex.Message}");
            }
        }

        private static void OnLayoutChange(string layoutName, InputControlLayoutChange change)
        {
            // CRITICAL: Prevent re-entrancy / recursion
            if (isApplying)
                return;

            if (string.Equals(layoutName, "DPad", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(layoutName, "Dpad", StringComparison.OrdinalIgnoreCase))
            {
                if (change == InputControlLayoutChange.Added || change == InputControlLayoutChange.Replaced)
                {
                    // Only intervene if what was registered is actually a device layout collision
                    try
                    {
                        var layout = InputSystem.LoadLayout(new InternedString(layoutName));
                        if (layout != null && layout.isDeviceLayout)
                        {
                            EnsureDpadControlLayout();
                        }
                    }
                    catch
                    {
                        EnsureDpadControlLayout();
                    }
                }
            }
        }

        private static void EnsureDpadControlLayout()
        {
            // Strict recursion guard
            if (isApplying)
                return;

            isApplying = true;
            try
            {
                bool isConflicted = false;

                try
                {
                    var currentLayout = InputSystem.LoadLayout(new InternedString("DPad"));
                    if (currentLayout != null && currentLayout.isDeviceLayout)
                    {
                        isConflicted = true;
                    }
                }
                catch
                {
                    // If loading threw or failed, assume conflict
                    isConflicted = true;
                }

                if (isConflicted)
                {
                    Debug.Log("[OpenXRInputLayoutFixer] Detected OpenXR 'DPad' device layout collision. Removing and restoring DpadControl...");

                    // Temporarily unhook listener to ensure RegisterLayout cannot trigger OnLayoutChange
                    InputSystem.onLayoutChange -= OnLayoutChange;

                    try
                    {
                        // 1. Remove conflicting layout
                        InputSystem.RemoveLayout("DPad");
                        InputSystem.RemoveLayout("Dpad");

                        // 2. Re-register default DpadControl for Dpad and DPad
                        InputSystem.RegisterLayout<DpadControl>("Dpad");
                        InputSystem.RegisterLayout<DpadControl>("DPad");
                        InputSystem.RegisterLayout<DpadControl.DpadAxisControl>("DpadAxis");

                        // 3. Re-register OpenXR DPad layout under non-conflicting name
                        try
                        {
                            var openXrDpadType = typeof(UnityEngine.XR.OpenXR.Features.Interactions.DPadInteraction.DPad);
                            if (openXrDpadType != null)
                            {
                                InputSystem.RegisterLayout(openXrDpadType, "OpenXR_DPad",
                                    matches: new InputDeviceMatcher()
                                        .WithInterface(UnityEngine.InputSystem.XR.XRUtilities.InterfaceMatchAnyVersion)
                                        .WithProduct("D-Pad Interaction"));
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[OpenXRInputLayoutFixer] OpenXR DPad secondary registration note: {ex.Message}");
                        }

                        Debug.Log("[OpenXRInputLayoutFixer] Successfully resolved OpenXR Dpad collision. DpadControl restored.");
                    }
                    finally
                    {
                        // Re-hook listener after all modifications are complete
                        InputSystem.onLayoutChange += OnLayoutChange;
                    }
                }
                // IMPORTANT: If NOT conflicted, DO NOT call RegisterLayout! Doing nothing preserves stability.
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OpenXRInputLayoutFixer] Layout correction error: {ex.Message}");
            }
            finally
            {
                isApplying = false;
            }
        }
    }
}
#endif
