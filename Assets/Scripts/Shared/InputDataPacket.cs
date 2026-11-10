// InputDataPacket.cs — Binary-serializable data structure for UDP transport
// Used by both the PC Companion sender and the HoloLens 2 receiver.
using System;

namespace AutoSim.Networking
{
    [Serializable]
    public struct InputDataPacket
    {
        // 4-byte ASCII header "ASIM" = 0x4D495341 little-endian
        public const uint HEADER_MAGIC = 0x4D495341;
        public const int PACKET_SIZE = 32; // 4+4+4+4+4+4+4+4 bytes

        public uint sequenceNumber;
        public float steering;   // -1 (full left) .. +1 (full right)
        public float throttle;   // 0 .. 1
        public float brake;      // 0 .. 1
        public float clutch;     // 0 .. 1
        public uint buttons;     // bitmask: bit0=paddleLeft, bit1=paddleRight, ...
        public float timestamp;  // sender-side Time.time or system clock

        /// <summary>Serialize to 32-byte little-endian binary packet.</summary>
        public byte[] Serialize()
        {
            byte[] data = new byte[PACKET_SIZE];
            int offset = 0;
            WriteUInt32(data, ref offset, HEADER_MAGIC);
            WriteUInt32(data, ref offset, sequenceNumber);
            WriteFloat(data, ref offset, steering);
            WriteFloat(data, ref offset, throttle);
            WriteFloat(data, ref offset, brake);
            WriteFloat(data, ref offset, clutch);
            WriteUInt32(data, ref offset, buttons);
            WriteFloat(data, ref offset, timestamp);
            return data;
        }

        /// <summary>Deserialize from 32-byte binary packet. Returns false if header mismatch.</summary>
        public static bool TryDeserialize(byte[] data, out InputDataPacket packet)
        {
            packet = default;
            if (data == null || data.Length < PACKET_SIZE)
                return false;

            int offset = 0;
            uint header = ReadUInt32(data, ref offset);
            if (header != HEADER_MAGIC)
                return false;

            packet.sequenceNumber = ReadUInt32(data, ref offset);
            packet.steering = ReadFloat(data, ref offset);
            packet.throttle = ReadFloat(data, ref offset);
            packet.brake = ReadFloat(data, ref offset);
            packet.clutch = ReadFloat(data, ref offset);
            packet.buttons = ReadUInt32(data, ref offset);
            packet.timestamp = ReadFloat(data, ref offset);
            return true;
        }

        // --- Binary helpers (little-endian) ---
        private static void WriteUInt32(byte[] buf, ref int offset, uint value)
        {
            buf[offset++] = (byte)(value & 0xFF);
            buf[offset++] = (byte)((value >> 8) & 0xFF);
            buf[offset++] = (byte)((value >> 16) & 0xFF);
            buf[offset++] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteFloat(byte[] buf, ref int offset, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, buf, offset, 4);
            offset += 4;
        }

        private static uint ReadUInt32(byte[] buf, ref int offset)
        {
            uint value = (uint)(buf[offset] | (buf[offset + 1] << 8) |
                                (buf[offset + 2] << 16) | (buf[offset + 3] << 24));
            offset += 4;
            return value;
        }

        private static float ReadFloat(byte[] buf, ref int offset)
        {
            float value = BitConverter.ToSingle(buf, offset);
            offset += 4;
            return value;
        }

        public override string ToString()
        {
            return $"[Seq:{sequenceNumber} Steer:{steering:F2} Thr:{throttle:F2} Brk:{brake:F2} Btn:0x{buttons:X}]";
        }
    }
}
