// LogitechSDKWrapper.cs — P/Invoke declarations for the Logitech Steering Wheel SDK.
// Desktop-only: the entire file is excluded from UWP builds via the #if guard.
#if !UNITY_WSA

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AutoSim.CompanionApp
{
    /// <summary>
    /// Low-level P/Invoke wrapper around <c>LogitechSteeringWheelEnginesWrapper.dll</c>.
    /// Also contains helper methods for marshalling <see cref="DIJOYSTATE2"/> and
    /// normalizing G920 axis values.
    /// </summary>
    public static class LogitechSDKWrapper
    {
        // ─────────────────────── DLL name ────────────────────────────
        private const string DLL = "LogitechSteeringWheelEnginesWrapper";

        // ─────────────────────── Lifecycle ────────────────────────────
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiSteeringInitialize(bool ignoreXInputControllers);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void LogiUpdate();

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern void LogiSteeringShutdown();

        // ─────────────────────── Device query ────────────────────────
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiIsConnected(int index);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr LogiGetState(int index);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiGetFriendlyProductName(
            int index, StringBuilder buffer, int bufferSize);

        // ─────────────────────── Operating range ─────────────────────
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiSetOperatingRange(int index, int range);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiGetOperatingRange(int index, ref int range);

        // ─────────────────────── Force feedback ──────────────────────
        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiPlaySpringForce(
            int index, int offsetPercentage, int saturationPercentage, int coefficientPercentage);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiStopSpringForce(int index);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiPlayConstantForce(int index, int magnitudePercentage);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiStopConstantForce(int index);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiPlayDamperForce(int index, int coefficientPercentage);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiStopDamperForce(int index);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiPlayBumpyRoadEffect(int index, int magnitudePercentage);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiStopBumpyRoadEffect(int index);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiPlayFrontalCollisionForce(int index, int magnitudePercentage);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiPlaySideCollisionForce(int index, int magnitudePercentage);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiPlaySurfaceEffect(int index, int type, int magnitudePercentage, int period);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiStopSurfaceEffect(int index);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiHasForceFeedback(int index);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool LogiIsPlaying(int index, int forceType);

        // ═════════════════════════════════════════════════════════════
        // DIJOYSTATE2 — DirectInput joystick state (280 bytes)
        // ═════════════════════════════════════════════════════════════
        [StructLayout(LayoutKind.Sequential)]
        public struct DIJOYSTATE2
        {
            public int lX;
            public int lY;
            public int lZ;
            public int lRx;
            public int lRy;
            public int lRz;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public int[] rglSlider;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public uint[] rgdwPOV;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public byte[] rgbButtons;

            public int lVX;
            public int lVY;
            public int lVZ;
            public int lVRx;
            public int lVRy;
            public int lVRz;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public int[] rglVSlider;

            public int lAX;
            public int lAY;
            public int lAZ;
            public int lARx;
            public int lARy;
            public int lARz;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public int[] rglASlider;

            public int lFX;
            public int lFY;
            public int lFZ;
            public int lFRx;
            public int lFRy;
            public int lFRz;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public int[] rglFSlider;
        }

        // ═════════════════════════════════════════════════════════════
        // Helper methods
        // ═════════════════════════════════════════════════════════════

        /// <summary>
        /// Marshals the native pointer returned by <see cref="LogiGetState"/> into a
        /// managed <see cref="DIJOYSTATE2"/>. Returns null if the pointer is invalid.
        /// </summary>
        public static DIJOYSTATE2? GetState(int index)
        {
            IntPtr ptr = LogiGetState(index);
            if (ptr == IntPtr.Zero)
                return null;

            return Marshal.PtrToStructure<DIJOYSTATE2>(ptr);
        }

        /// <summary>
        /// Normalizes the raw <c>lX</c> steering axis from the Logitech SDK
        /// (range -32768 … +32767) to a float in the range -1 … +1.
        /// </summary>
        /// <param name="rawLX">The <c>DIJOYSTATE2.lX</c> value.</param>
        /// <returns>Normalized steering: -1 = full left, +1 = full right.</returns>
        public static float NormalizeSteering(int rawLX)
        {
            return Clamp01Signed(rawLX / 32767f);
        }

        /// <summary>
        /// Normalizes a G920 pedal axis to a 0…1 range.
        /// The Logitech G920 reports pedals as:
        ///   +32767 = fully released (unpressed)
        ///   -32768 = fully pressed
        /// </summary>
        /// <param name="rawAxis">
        /// The raw axis value (<c>lY</c> for throttle, <c>lRz</c> for brake, <c>lZ</c> for clutch).
        /// </param>
        /// <returns>0 = released, 1 = fully pressed.</returns>
        public static float NormalizePedal(int rawAxis)
        {
            // Map [+32767 .. -32768] → [0 .. 1]
            float normalized = (32767f - rawAxis) / 65535f;
            return Clamp01(normalized);
        }

        /// <summary>
        /// Reads button states from <see cref="DIJOYSTATE2.rgbButtons"/> and packs them
        /// into a <c>uint</c> bitmask compatible with <see cref="AutoSimConstants"/> button
        /// constants. Only the first 24 buttons are mapped.
        /// </summary>
        public static uint PackButtons(DIJOYSTATE2 state)
        {
            if (state.rgbButtons == null) return 0u;

            uint mask = 0u;
            int count = Math.Min(state.rgbButtons.Length, 24);
            for (int i = 0; i < count; i++)
            {
                // DirectInput reports 0x80 when pressed.
                if ((state.rgbButtons[i] & 0x80) != 0)
                    mask |= (1u << i);
            }
            return mask;
        }

        // ═════════════════════════════════════════════════════════════
        // Safe Force Feedback Execution (with DllNotFoundException guards)
        // ═════════════════════════════════════════════════════════════

        private static bool? _isNativeDllAvailable = null;

        /// <summary>Checks whether LogitechSteeringWheelEnginesWrapper.dll is present and accessible.</summary>
        public static bool IsNativeDllAvailable
        {
            get
            {
                if (_isNativeDllAvailable.HasValue)
                    return _isNativeDllAvailable.Value;

                try
                {
                    _isNativeDllAvailable = LogiIsConnected(0);
                    return _isNativeDllAvailable.Value;
                }
                catch (DllNotFoundException)
                {
                    _isNativeDllAvailable = false;
                    return false;
                }
                catch (Exception)
                {
                    _isNativeDllAvailable = false;
                    return false;
                }
            }
        }

        /// <summary>Plays a short impact/shock haptic impulse (0-100%).</summary>
        public static bool PlayImpactForceSafe(int index, int magnitudePercentage)
        {
            if (!IsNativeDllAvailable) return false;
            try
            {
                magnitudePercentage = Math.Max(0, Math.Min(100, magnitudePercentage));
                return LogiPlayFrontalCollisionForce(index, magnitudePercentage);
            }
            catch { return false; }
        }

        /// <summary>Plays a bumpy road / curb effect (0-100%).</summary>
        public static bool PlayBumpyRoadSafe(int index, int magnitudePercentage)
        {
            if (!IsNativeDllAvailable) return false;
            try
            {
                magnitudePercentage = Math.Max(0, Math.Min(100, magnitudePercentage));
                return LogiPlayBumpyRoadEffect(index, magnitudePercentage);
            }
            catch { return false; }
        }

        /// <summary>Stops bumpy road effect.</summary>
        public static bool StopBumpyRoadSafe(int index)
        {
            if (!IsNativeDllAvailable) return false;
            try { return LogiStopBumpyRoadEffect(index); } catch { return false; }
        }

        /// <summary>Plays a spring centering force on the steering wheel.</summary>
        public static bool PlayCenteringSpringSafe(int index, int offset, int saturation, int coefficient)
        {
            if (!IsNativeDllAvailable) return false;
            try { return LogiPlaySpringForce(index, offset, saturation, coefficient); } catch { return false; }
        }

        /// <summary>Stops all active forces on the specified wheel index.</summary>
        public static void StopAllForcesSafe(int index)
        {
            if (!IsNativeDllAvailable) return;
            try
            {
                LogiStopBumpyRoadEffect(index);
                LogiStopConstantForce(index);
                LogiStopDamperForce(index);
            }
            catch { }
        }

        // ── Tiny math helpers (avoid pulling in Mathf on non-Unity builds) ──
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        private static float Clamp01Signed(float v) => v < -1f ? -1f : (v > 1f ? 1f : v);
    }
}

#endif // !UNITY_WSA
