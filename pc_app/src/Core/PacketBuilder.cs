using System;
using GamepadEmulator.Models;

namespace GamepadEmulator.Core
{
    public class PacketBuilder
    {
        public const byte NormalHeader = 0x5A;
        public const byte CommandHeader = 0xA5;

        private static readonly byte[] s_crc8Table = InitializeCrc8Table();

        private readonly byte[] _buffer = new byte[6];

        private static byte[] InitializeCrc8Table()
        {
            const byte polynomial = 0x07; // CRC-8/SMBUS: x^8 + x^2 + x + 1
            byte[] table = new byte[256];

            for (int i = 0; i < 256; i++)
            {
                byte curr = (byte)i;
                for (int j = 0; j < 8; j++)
                {
                    if ((curr & 0x80) != 0)
                    {
                        curr = (byte)((curr << 1) ^ polynomial);
                    }
                    else
                    {
                        curr = (byte)(curr << 1);
                    }
                }
                table[i] = curr;
            }

            return table;
        }

        public static byte ComputeCrc8(ReadOnlySpan<byte> data)
        {
            byte crc = 0x00;
            for (int i = 0; i < data.Length; i++)
            {
                crc = s_crc8Table[crc ^ data[i]];
            }
            return crc;
        }

        public ReadOnlySpan<byte> BuildPacket(in InputState state, bool modeOn, bool guideLocked)
        {
            // Byte 0: Normal input header
            _buffer[0] = NormalHeader;

            // Movement nibble: bit0: Up, bit1: Down, bit2: Left, bit3: Right
            byte moveNibble = 0;
            if (state.Up) moveNibble |= 0x01;
            if (state.Down) moveNibble |= 0x02;
            if (state.Left) moveNibble |= 0x04;
            if (state.Right) moveNibble |= 0x08;

            // Byte 1: D-Pad & System buttons
            byte byte1 = 0;
            if (!modeOn)
            {
                byte1 |= moveNibble; // D-Pad receives movement
            }
            if (state.Start) byte1 |= 0x10;
            if (state.Back) byte1 |= 0x20;
            if (state.Guide && !guideLocked) byte1 |= 0x40;
            _buffer[1] = byte1;

            // Byte 2: Action buttons
            byte byte2 = 0;
            if (state.A) byte2 |= 0x01;
            if (state.B) byte2 |= 0x02;
            if (state.X) byte2 |= 0x04;
            if (state.Y) byte2 |= 0x08;
            if (state.LB) byte2 |= 0x10;
            if (state.RB) byte2 |= 0x20;
            if (state.L3) byte2 |= 0x40;
            if (state.R3) byte2 |= 0x80;
            _buffer[2] = byte2;

            // Byte 3: Stick axes (Lower 4bit: Left Stick, Upper 4bit: Right Stick)
            byte byte3 = 0;
            if (modeOn)
            {
                byte3 |= moveNibble; // Left Stick receives movement
            }
            if (state.RsUp) byte3 |= 0x10;
            if (state.RsDown) byte3 |= 0x20;
            if (state.RsLeft) byte3 |= 0x40;
            if (state.RsRight) byte3 |= 0x80;
            _buffer[3] = byte3;

            // Byte 4: Triggers (Digital 4bit each: LT upper 4bit, RT lower 4bit)
            byte byte4 = 0;
            if (state.LT) byte4 |= 0xF0; // 15 in upper 4 bits
            if (state.RT) byte4 |= 0x0F; // 15 in lower 4 bits
            _buffer[4] = byte4;

            // Byte 5: CRC-8 over Byte 0 to Byte 4
            _buffer[5] = ComputeCrc8(_buffer.AsSpan(0, 5));

            return _buffer;
        }

        private static readonly InputState s_neutralState = default;

        public ReadOnlySpan<byte> BuildNeutralPacket()
        {
            return BuildPacket(in s_neutralState, modeOn: false, guideLocked: true);
        }
    }
}
