using System;
using System.Collections.Generic;
using System.Text;
using GamepadEmulator.Core;
using GamepadEmulator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GamepadEmulator.Tests
{
    [TestClass]
    public class PacketBuilderTests
    {
        private PacketBuilder _builder = null!;
        private SocdCleaner _socdCleaner = null!;

        [TestInitialize]
        public void Setup()
        {
            _builder = new PacketBuilder();
            _socdCleaner = new SocdCleaner();
        }

        [TestMethod]
        public void VerifyCrc8_StandardTestVector_MatchesF4()
        {
            // Standard test vector for CRC-8/SMBUS polynomial 0x07 (initial 0x00, RefIn/RefOut false, XorOut 0x00)
            byte[] input = Encoding.ASCII.GetBytes("123456789");
            byte crc = PacketBuilder.ComputeCrc8(input);

            Assert.AreEqual(0xF4, crc, $"Expected 0xF4 for test vector '123456789', but got 0x{crc:X2}");
        }

        [TestMethod]
        public void VerifyCrc8_AllKeysReleased_Matches0x27()
        {
            // Payload 5A 00 00 00 00
            byte[] neutralPayload = new byte[] { 0x5A, 0x00, 0x00, 0x00, 0x00 };
            byte crc = PacketBuilder.ComputeCrc8(neutralPayload);

            Assert.AreEqual(0x27, crc, $"Expected CRC 0x27 for 5A 00 00 00 00, but got 0x{crc:X2}");

            // Verify BuildPacket returns 5A 00 00 00 00 27
            InputState state = default;
            ReadOnlySpan<byte> packet = _builder.BuildPacket(in state, modeOn: false, guideLocked: true);

            Assert.AreEqual(6, packet.Length);
            Assert.AreEqual(0x5A, packet[0]);
            Assert.AreEqual(0x00, packet[1]);
            Assert.AreEqual(0x00, packet[2]);
            Assert.AreEqual(0x00, packet[3]);
            Assert.AreEqual(0x00, packet[4]);
            Assert.AreEqual(0x27, packet[5]);
        }

        [TestMethod]
        public void VerifySocd_NeutralArbitration_CPTCompliant()
        {
            InputState state = default;
            // Left + Right pressed simultaneously
            state.Left = true;
            state.Right = true;

            // Up + Down pressed simultaneously
            state.Up = true;
            state.Down = true;

            _socdCleaner.Clean(ref state);

            Assert.IsFalse(state.Left, "Left must be cleared by SOCD neutral cleaner");
            Assert.IsFalse(state.Right, "Right must be cleared by SOCD neutral cleaner");
            Assert.IsFalse(state.Up, "Up must be cleared by SOCD neutral cleaner");
            Assert.IsFalse(state.Down, "Down must be cleared by SOCD neutral cleaner");

            ReadOnlySpan<byte> packet = _builder.BuildPacket(in state, modeOn: false, guideLocked: true);
            Assert.AreEqual(0x00, packet[1] & 0x0F, "D-Pad direction nibble must be 0 after SOCD cleaning");
        }

        [TestMethod]
        public void VerifyModeSwitch_OutputByteRouting()
        {
            InputState state = default;
            state.Up = true;
            state.Right = true;

            // MODE OFF: Direction output to Byte 1 (D-Pad bit0 and bit3 -> 0x01 | 0x08 = 0x09)
            ReadOnlySpan<byte> packetModeOff = _builder.BuildPacket(in state, modeOn: false, guideLocked: true);
            Assert.AreEqual(0x09, packetModeOff[1] & 0x0F, "Mode OFF: D-Pad must receive movement keys (Up + Right = 0x09)");
            Assert.AreEqual(0x00, packetModeOff[3] & 0x0F, "Mode OFF: Left Stick must be zero");

            // MODE ON: Direction output to Byte 3 lower nibble (Left stick bit0 and bit3 -> 0x01 | 0x08 = 0x09)
            ReadOnlySpan<byte> packetModeOn = _builder.BuildPacket(in state, modeOn: true, guideLocked: true);
            Assert.AreEqual(0x00, packetModeOn[1] & 0x0F, "Mode ON: D-Pad must be zero");
            Assert.AreEqual(0x09, packetModeOn[3] & 0x0F, "Mode ON: Left Stick must receive movement keys (Up + Right = 0x09)");
        }

        [TestMethod]
        public void VerifyGuideLock_MasksBit6InByte1()
        {
            InputState state = default;
            state.Guide = true;

            // Guide Locked = true -> bit 6 should be 0
            ReadOnlySpan<byte> lockedPacket = _builder.BuildPacket(in state, modeOn: false, guideLocked: true);
            Assert.AreEqual(0x00, lockedPacket[1] & 0x40, "Guide bit (bit6) must be masked to 0 when GuideLocked is true");

            // Guide Locked = false -> bit 6 should be 1 (0x40)
            ReadOnlySpan<byte> unlockedPacket = _builder.BuildPacket(in state, modeOn: false, guideLocked: false);
            Assert.AreEqual(0x40, unlockedPacket[1] & 0x40, "Guide bit (bit6) must be set when GuideLocked is false");
        }

        [TestMethod]
        public void VerifyTriggers_DigitalOperationInByte4()
        {
            InputState state = default;
            state.LT = true;
            state.RT = false;

            ReadOnlySpan<byte> ltOnly = _builder.BuildPacket(in state, modeOn: false, guideLocked: true);
            Assert.AreEqual(0xF0, ltOnly[4], "LT pressed should result in 0xF0 in Byte 4");

            state.LT = false;
            state.RT = true;
            ReadOnlySpan<byte> rtOnly = _builder.BuildPacket(in state, modeOn: false, guideLocked: true);
            Assert.AreEqual(0x0F, rtOnly[4], "RT pressed should result in 0x0F in Byte 4");

            state.LT = true;
            state.RT = true;
            ReadOnlySpan<byte> bothTriggers = _builder.BuildPacket(in state, modeOn: false, guideLocked: true);
            Assert.AreEqual(0xFF, bothTriggers[4], "Both triggers pressed should result in 0xFF in Byte 4");
        }

        [TestMethod]
        public void VerifyActionButtons_BitAllocationsInByte2()
        {
            InputState state = default;
            state.A = true;
            state.B = true;
            state.X = true;
            state.Y = true;
            state.LB = true;
            state.RB = true;
            state.L3 = true;
            state.R3 = true;

            ReadOnlySpan<byte> packet = _builder.BuildPacket(in state, modeOn: false, guideLocked: true);
            Assert.AreEqual(0xFF, packet[2], "Byte 2 should have all bits set (0xFF) when all action buttons are pressed");
        }

        [TestMethod]
        [DataRow((byte)0x01, (byte)0xFE, "Mode B command")]
        [DataRow((byte)0x02, (byte)0xFD, "Mode A command")]
        [DataRow((byte)0x03, (byte)0xFC, "P1 Reset command")]
        public void VerifySpecialControlCommands_HeaderAndInverseCheck(byte cmdId, byte expectedInverse, string description)
        {
            byte[] cmdBuf = new byte[6];
            cmdBuf[0] = PacketBuilder.CommandHeader; // 0xA5
            cmdBuf[1] = cmdId;
            cmdBuf[2] = (byte)(~cmdId);
            cmdBuf[3] = 0x00;
            cmdBuf[4] = 0x00;
            cmdBuf[5] = PacketBuilder.ComputeCrc8(cmdBuf.AsSpan(0, 5));

            Assert.AreEqual(0xA5, cmdBuf[0]);
            Assert.AreEqual(cmdId, cmdBuf[1]);
            Assert.AreEqual(expectedInverse, cmdBuf[2], $"Inverse check failed for {description}");
            Assert.AreEqual(0x00, cmdBuf[3]);
            Assert.AreEqual(0x00, cmdBuf[4]);

            // Recompute CRC and verify consistency
            byte recomputedCrc = PacketBuilder.ComputeCrc8(cmdBuf.AsSpan(0, 5));
            Assert.AreEqual(recomputedCrc, cmdBuf[5], $"CRC-8 mismatch for {description}");
        }

        [TestMethod]
        public void VerifySocd_SustainedHold_MaintainsNeutral()
        {
            using RawInputReceiver receiver = new();
            byte[]? lastReceivedPacket = null;
            InputState lastState = default;

            receiver.OnPacketGenerated += (ReadOnlySpan<byte> packet, in InputState state) =>
            {
                lastReceivedPacket = packet.ToArray();
                lastState = state;
            };

            // VK_A = 0x41 (Left in default bindings), VK_D = 0x44 (Right in default bindings)
            const ushort vkA = 0x41;
            const ushort vkD = 0x44;

            // 1. A Key Down (Make)
            receiver.HandleKeyEvent(vkA, isMake: true);
            Assert.IsNotNull(lastReceivedPacket);
            Assert.AreEqual(0x04, lastReceivedPacket[1] & 0x0F, "Left should be active (bit2 = 0x04)");
            Assert.IsTrue(lastState.Left);
            Assert.IsFalse(lastState.Right);
            Assert.IsTrue(receiver.CurrentRawState.Left);
            Assert.IsFalse(receiver.CurrentRawState.Right);

            // 2. D Key Down (Make) -> Left + Right simultaneous hold -> Neutral
            receiver.HandleKeyEvent(vkD, isMake: true);
            Assert.AreEqual(0x00, lastReceivedPacket[1] & 0x0F, "Left + Right simultaneously held must result in Neutral (0x00)");
            Assert.IsFalse(lastState.Left, "Cleaned Left must be false");
            Assert.IsFalse(lastState.Right, "Cleaned Right must be false");
            Assert.IsTrue(receiver.CurrentRawState.Left, "Raw Left must remain true while physically held");
            Assert.IsTrue(receiver.CurrentRawState.Right, "Raw Right must remain true while physically held");

            // 3. Autorepeat Make for A arrives multiple times while both keys held
            for (int i = 0; i < 5; i++)
            {
                receiver.HandleKeyEvent(vkA, isMake: true);
                Assert.AreEqual(0x00, lastReceivedPacket[1] & 0x0F, $"Autorepeat A cycle {i}: packet must remain Neutral");
                Assert.IsTrue(receiver.CurrentRawState.Left, $"Autorepeat A cycle {i}: Raw Left must remain true");
                Assert.IsTrue(receiver.CurrentRawState.Right, $"Autorepeat A cycle {i}: Raw Right must remain true");
            }

            // 4. Autorepeat Make for D arrives multiple times while both keys held
            for (int i = 0; i < 5; i++)
            {
                receiver.HandleKeyEvent(vkD, isMake: true);
                Assert.AreEqual(0x00, lastReceivedPacket[1] & 0x0F, $"Autorepeat D cycle {i}: packet must remain Neutral");
                Assert.IsTrue(receiver.CurrentRawState.Left, $"Autorepeat D cycle {i}: Raw Left must remain true");
                Assert.IsTrue(receiver.CurrentRawState.Right, $"Autorepeat D cycle {i}: Raw Right must remain true");
            }

            // 5. Finally, release A Key (Break) -> D Key remains held -> Right output immediately
            receiver.HandleKeyEvent(vkA, isMake: false);
            Assert.AreEqual(0x08, lastReceivedPacket[1] & 0x0F, "Releasing A must immediately output Right (bit3 = 0x08)");
            Assert.IsFalse(lastState.Left);
            Assert.IsTrue(lastState.Right);
            Assert.IsFalse(receiver.CurrentRawState.Left);
            Assert.IsTrue(receiver.CurrentRawState.Right);

            // 6. Release D Key (Break) -> All released -> Neutral
            receiver.HandleKeyEvent(vkD, isMake: false);
            Assert.AreEqual(0x00, lastReceivedPacket[1] & 0x0F, "Releasing D must return to Neutral");
            Assert.IsFalse(lastState.Left);
            Assert.IsFalse(lastState.Right);
            Assert.IsFalse(receiver.CurrentRawState.Left);
            Assert.IsFalse(receiver.CurrentRawState.Right);
        }

        [TestMethod]
        public void VerifyDeadmanCheck_DoesNotClear_WhenKeyHeld()
        {
            InputState rawState = default;
            rawState.Left = true;
            rawState.Right = true;

            HashSet<int> pressedKeys = new() { 0x41, 0x44 }; // A and D
            Dictionary<int, GamepadButton> bindings = new()
            {
                { 0x41, GamepadButton.Left },
                { 0x44, GamepadButton.Right }
            };

            // Simulate GetAsyncKeyState returning 0x8000 (key held)
            Func<int, short> mockGetAsyncKeyState = vKey => unchecked((short)0x8000);

            // Run deadman check multiple times (e.g. 10 cycles representing 80ms)
            for (int i = 0; i < 10; i++)
            {
                bool changed = _socdCleaner.PerformDeadMansCheck(ref rawState, bindings, pressedKeys, mockGetAsyncKeyState);
                Assert.IsFalse(changed, $"Cycle {i}: Deadman check should not report state changed when keys are held");
                Assert.IsTrue(rawState.Left, $"Cycle {i}: Left raw state must remain true");
                Assert.IsTrue(rawState.Right, $"Cycle {i}: Right raw state must remain true");
                Assert.IsTrue(pressedKeys.Contains(0x41), $"Cycle {i}: PressedKeys must retain A");
                Assert.IsTrue(pressedKeys.Contains(0x44), $"Cycle {i}: PressedKeys must retain D");
            }

            // Now simulate releasing A (returns 0) while D is still held (returns 0x8000)
            Func<int, short> mockReleaseA = vKey => (vKey == 0x41) ? (short)0 : unchecked((short)0x8000);

            bool releaseChanged = _socdCleaner.PerformDeadMansCheck(ref rawState, bindings, pressedKeys, mockReleaseA);
            Assert.IsTrue(releaseChanged, "Deadman check must detect release of A");
            Assert.IsFalse(rawState.Left, "Left must be cleared after physical release");
            Assert.IsTrue(rawState.Right, "Right must remain pressed");
            Assert.IsFalse(pressedKeys.Contains(0x41), "PressedKeys must no longer contain A");
            Assert.IsTrue(pressedKeys.Contains(0x44), "PressedKeys must retain D");
        }
    }
}

