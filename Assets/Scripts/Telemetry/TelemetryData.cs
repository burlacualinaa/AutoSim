// TelemetryData.cs — One frame of driving + eye tracking telemetry for CSV recording
using System.Globalization;
using UnityEngine;

namespace AutoSim.Telemetry
{
    [System.Serializable]
    public struct TelemetryFrame
    {
        // ---- Timestamp ----
        public double sessionTime;     // seconds since recording started
        public int frameNumber;
        public string systemTimestamp; // ISO 8601

        // ---- Driver Input ----
        public float steering;         // -1..+1
        public float throttle;         // 0..1
        public float brake;            // 0..1
        public float clutch;           // 0..1

        // ---- Vehicle State ----
        public float posX, posY, posZ;
        public float rotX, rotY, rotZ; // euler angles
        public float speedKmh;
        public float accelerationMps2;
        public float lateralG;

        // ---- Wheel Data ----
        public float wheelRpmFL, wheelRpmFR, wheelRpmRL, wheelRpmRR;

        // ---- Eye Tracking ----
        public float gazeOriginX, gazeOriginY, gazeOriginZ;
        public float gazeDirX, gazeDirY, gazeDirZ;
        public bool gazeValid;
        public string gazedObjectName;
        public float gazeHitDistance;

        // ---- Network ----
        public int networkPacketsReceived;
        public float networkLatencyMs;

        // ---- Scenario ----
        public string activeScenario;  // e.g., "PedestrianCrossing", "None"
        public float distanceToHazard;

        /// <summary>CSV header row matching ToCsvRow() field order.</summary>
        public static string CsvHeader()
        {
            return "SessionTime,FrameNumber,SystemTimestamp," +
                   "Steering,Throttle,Brake,Clutch," +
                   "PosX,PosY,PosZ,RotX,RotY,RotZ," +
                   "SpeedKmH,AccelerationMps2,LateralG," +
                   "WheelRpmFL,WheelRpmFR,WheelRpmRL,WheelRpmRR," +
                   "GazeOriginX,GazeOriginY,GazeOriginZ," +
                   "GazeDirX,GazeDirY,GazeDirZ," +
                   "GazeValid,GazedObject,GazeHitDistance," +
                   "NetPacketsRx,NetLatencyMs," +
                   "ActiveScenario,DistanceToHazard";
        }

        /// <summary>Convert to one CSV row (no newline).</summary>
        public string ToCsvRow()
        {
            var c = CultureInfo.InvariantCulture;
            return string.Format(c,
                "{0:F4},{1},{2}," +
                "{3:F4},{4:F4},{5:F4},{6:F4}," +
                "{7:F3},{8:F3},{9:F3},{10:F2},{11:F2},{12:F2}," +
                "{13:F2},{14:F3},{15:F3}," +
                "{16:F1},{17:F1},{18:F1},{19:F1}," +
                "{20:F4},{21:F4},{22:F4}," +
                "{23:F4},{24:F4},{25:F4}," +
                "{26},{27},{28:F3}," +
                "{29},{30:F2}," +
                "{31},{32:F2}",
                sessionTime, frameNumber, systemTimestamp ?? "",
                steering, throttle, brake, clutch,
                posX, posY, posZ, rotX, rotY, rotZ,
                speedKmh, accelerationMps2, lateralG,
                wheelRpmFL, wheelRpmFR, wheelRpmRL, wheelRpmRR,
                gazeOriginX, gazeOriginY, gazeOriginZ,
                gazeDirX, gazeDirY, gazeDirZ,
                gazeValid ? 1 : 0, EscapeCsv(gazedObjectName), gazeHitDistance,
                networkPacketsReceived, networkLatencyMs,
                EscapeCsv(activeScenario), distanceToHazard);
        }

        private static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(",") || s.Contains("\""))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
