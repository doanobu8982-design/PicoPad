using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using GamepadEmulator.Models;

namespace GamepadEmulator.Core
{
    public delegate void PacketGeneratedCallback(ReadOnlySpan<byte> packet, in InputState state);

    public class RawInputReceiver : IDisposable
    {
        private const string WindowClassName = "GamepadEmulator_RawInput_MsgWnd";
        private const uint MAPVK_VSC_TO_VK_EX = 3;

        private readonly PacketBuilder _packetBuilder = new();
        private readonly SocdCleaner _socdCleaner = new();
        private readonly HashSet<int> _pressedPhysicalKeys = new();
        private readonly byte[] _lastPayload = new byte[5];

        private Dictionary<int, List<GamepadButton>> _multiBindings = new();
        private Dictionary<int, List<GamepadButton>> _keyToButtons => _multiBindings;
        private InputState _rawInputState;

        public InputState CurrentRawState => _rawInputState;

        private Thread? _workerThread;
        private IntPtr _hWnd = IntPtr.Zero;
        private IntPtr _rawInputBuffer = IntPtr.Zero;
        private uint _rawInputBufferSize = 256;
        private Win32Native.WndProc? _wndProcDelegate;

        private volatile bool _isRunning;
        private volatile bool _disposed;
        private volatile bool _modeOn;
        private volatile bool _guideLocked = true;

        public RawInputReceiver()
        {
            SetProfile(GamepadProfile.CreateDefault());
        }

        private long _lastSendTimestamp;

        public event PacketGeneratedCallback? OnPacketGenerated;

        public bool ModeOn
        {
            get => _modeOn;
            set
            {
                if (_modeOn != value)
                {
                    _modeOn = value;
                    if (_isRunning && _hWnd != IntPtr.Zero)
                    {
                        // Notify thread to process mode change safely
                        Win32Native.PostMessage(_hWnd, Win32Native.WM_USER + 1, IntPtr.Zero, IntPtr.Zero);
                    }
                }
            }
        }

        public bool GuideLocked
        {
            get => _guideLocked;
            set
            {
                if (_guideLocked != value)
                {
                    _guideLocked = value;
                    if (_isRunning && _hWnd != IntPtr.Zero)
                    {
                        Win32Native.PostMessage(_hWnd, Win32Native.WM_USER + 2, IntPtr.Zero, IntPtr.Zero);
                    }
                }
            }
        }

        public void SetProfile(GamepadProfile? profile)
        {
            if (profile == null)
            {
                _modeOn = false;
                _guideLocked = false;
                _multiBindings = new Dictionary<int, List<GamepadButton>>();
                _rawInputState.Reset();
                _pressedPhysicalKeys.Clear();

                if (_isRunning && _hWnd != IntPtr.Zero)
                {
                    Win32Native.PostMessage(_hWnd, Win32Native.WM_USER + 1, IntPtr.Zero, IntPtr.Zero);
                }
                return;
            }

            _modeOn = profile.IsStickMode;
            _guideLocked = false;

            var multi = new Dictionary<int, List<GamepadButton>>();

            if (profile.Bindings != null)
            {
                foreach (var kvp in profile.Bindings)
                {
                    if (Enum.TryParse<GamepadButton>(kvp.Key, ignoreCase: true, out var button) && button != GamepadButton.None)
                    {
                        if (kvp.Value == null) continue;

                        foreach (int vk in kvp.Value)
                        {
                            if (vk <= 0) continue; // Unassigned

                            if (!multi.TryGetValue(vk, out var list))
                            {
                                list = new List<GamepadButton>();
                                multi[vk] = list;
                            }
                            if (!list.Contains(button))
                            {
                                list.Add(button);
                            }
                        }
                    }
                }
            }

            _multiBindings = multi;

            // Recalculate _rawInputState based on currently held physical keys and new bindings
            _rawInputState.Reset();
            foreach (int pk in _pressedPhysicalKeys)
            {
                if (multi.TryGetValue(pk, out var bList))
                {
                    foreach (var b in bList)
                    {
                        _rawInputState.SetButton(b, true);
                    }
                }
            }

            if (_isRunning && _hWnd != IntPtr.Zero)
            {
                Win32Native.PostMessage(_hWnd, Win32Native.WM_USER + 1, IntPtr.Zero, IntPtr.Zero);
            }
        }



        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _rawInputBuffer = Marshal.AllocHGlobal((int)_rawInputBufferSize);

            _workerThread = new Thread(WorkerThreadProc)
            {
                Name = "RawInputReceiverThread",
                Priority = ThreadPriority.Highest,
                IsBackground = true
            };
            _workerThread.Start();
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            if (_hWnd != IntPtr.Zero)
            {
                Win32Native.PostMessage(_hWnd, Win32Native.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }

            if (_workerThread != null && _workerThread.IsAlive)
            {
                _workerThread.Join(1000);
                _workerThread = null;
            }
        }

        private void WorkerThreadProc()
        {
            Win32Native.TimeBeginPeriod(1);
            try
            {
                RegisterWindowClass();
                CreateMessageWindow();
                RegisterRawInput();

                _lastSendTimestamp = Stopwatch.GetTimestamp();

                // Initial packet emit
                EmitCurrentPacket(force: true);

                while (_isRunning && !_disposed)
                {
                    long now = Stopwatch.GetTimestamp();
                    double elapsedMs = (now - _lastSendTimestamp) * 1000.0 / Stopwatch.Frequency;
                    uint timeoutMs = 8;
                    if (elapsedMs < 8.0)
                    {
                        timeoutMs = (uint)Math.Max(0, (int)Math.Ceiling(8.0 - elapsedMs));
                    }
                    else
                    {
                        timeoutMs = 0;
                    }

                    uint waitRes = Win32Native.MsgWaitForMultipleObjectsEx(
                        0, null, timeoutMs,
                        Win32Native.QS_ALLINPUT,
                        Win32Native.MWMO_INPUTAVAILABLE);

                    // Process message queue
                    while (Win32Native.PeekMessage(out Win32Native.MSG msg, IntPtr.Zero, 0, 0, Win32Native.PM_REMOVE))
                    {
                        if (msg.message == Win32Native.WM_QUIT)
                        {
                            return;
                        }

                        Win32Native.TranslateMessage(ref msg);
                        Win32Native.DispatchMessage(ref msg);
                    }

                    // Check 8ms keep-alive / dead man's check
                    now = Stopwatch.GetTimestamp();
                    elapsedMs = (now - _lastSendTimestamp) * 1000.0 / Stopwatch.Frequency;
                    if (elapsedMs >= 8.0)
                    {
                        bool stateChanged = _socdCleaner.PerformDeadMansCheck(ref _rawInputState, _multiBindings, _pressedPhysicalKeys);
                        EmitCurrentPacket(force: true);
                    }
                }
            }
            finally
            {
                UnregisterRawInput();
                DestroyMessageWindow();
                UnregisterWindowClass();
                Win32Native.TimeEndPeriod(1);
            }
        }

        private void RegisterWindowClass()
        {
            _wndProcDelegate = WndProc;
            Win32Native.WNDCLASSEX wc = new()
            {
                cbSize = (uint)Marshal.SizeOf<Win32Native.WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                hInstance = Win32Native.GetModuleHandle(null),
                lpszClassName = WindowClassName
            };
            Win32Native.RegisterClassEx(ref wc);
        }

        private void UnregisterWindowClass()
        {
            Win32Native.UnregisterClass(WindowClassName, Win32Native.GetModuleHandle(null));
        }

        private void CreateMessageWindow()
        {
            _hWnd = Win32Native.CreateWindowEx(
                0,
                WindowClassName,
                "RawInputReceiver",
                0,
                0, 0, 0, 0,
                Win32Native.HWND_MESSAGE,
                IntPtr.Zero,
                Win32Native.GetModuleHandle(null),
                IntPtr.Zero);
        }

        private void DestroyMessageWindow()
        {
            if (_hWnd != IntPtr.Zero)
            {
                Win32Native.DestroyWindow(_hWnd);
                _hWnd = IntPtr.Zero;
            }
        }

        private void RegisterRawInput()
        {
            if (_hWnd == IntPtr.Zero) return;

            Win32Native.RAWINPUTDEVICE[] rid = new Win32Native.RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01; // Generic Desktop Controls
            rid[0].usUsage = 0x06;     // Keyboard
            rid[0].dwFlags = Win32Native.RIDEV_INPUTSINK;
            rid[0].hwndTarget = _hWnd;

            Win32Native.RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf<Win32Native.RAWINPUTDEVICE>());
        }

        private void UnregisterRawInput()
        {
            Win32Native.RAWINPUTDEVICE[] rid = new Win32Native.RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01;
            rid[0].usUsage = 0x06;
            rid[0].dwFlags = Win32Native.RIDEV_REMOVE;
            rid[0].hwndTarget = IntPtr.Zero;

            Win32Native.RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf<Win32Native.RAWINPUTDEVICE>());
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case Win32Native.WM_INPUT:
                    ProcessRawInput(lParam);
                    return IntPtr.Zero;

                case Win32Native.WM_USER + 1: // Mode change
                    if (_socdCleaner.NotifyMode(_modeOn))
                    {
                        // 1. Send all-neutral safe packet first
                        ReadOnlySpan<byte> neutralPacket = _packetBuilder.BuildNeutralPacket();
                        OnPacketGenerated?.Invoke(neutralPacket, default);

                        // 2. Clear safe neutral request
                        _socdCleaner.SafeNeutralRequired = false;

                        // 3. Emit packet with updated mode
                        EmitCurrentPacket(force: true);
                    }
                    return IntPtr.Zero;

                case Win32Native.WM_USER + 2: // Guide locked toggle
                    EmitCurrentPacket(force: true);
                    return IntPtr.Zero;

                case Win32Native.WM_DESTROY:
                    return IntPtr.Zero;

                default:
                    return Win32Native.DefWindowProc(hWnd, msg, wParam, lParam);
            }
        }

        private void ProcessRawInput(IntPtr hRawInput)
        {
            uint dwSize = _rawInputBufferSize;
            uint res = Win32Native.GetRawInputData(
                hRawInput,
                Win32Native.RID_INPUT,
                _rawInputBuffer,
                ref dwSize,
                (uint)Marshal.SizeOf<Win32Native.RAWINPUTHEADER>());

            if (res == uint.MaxValue || res == 0) return;

            // Header offset: 24 on 64-bit architecture
            int keyboardOffset = Marshal.OffsetOf<Win32Native.RAWINPUT64>(nameof(Win32Native.RAWINPUT64.keyboard)).ToInt32();
            Win32Native.RAWKEYBOARD rawKeyboard = Marshal.PtrToStructure<Win32Native.RAWKEYBOARD>(_rawInputBuffer + keyboardOffset);

            ushort vKey = rawKeyboard.VKey;
            ushort flags = rawKeyboard.Flags;
            ushort makeCode = rawKeyboard.MakeCode;

            if (vKey == 0 || vKey == 0xFF) return;

            // Distinguish Left / Right for Shift, Ctrl, Alt
            if (vKey == 0x10) // VK_SHIFT
            {
                vKey = (ushort)MapVirtualKeyEx(makeCode, MAPVK_VSC_TO_VK_EX);
                if (vKey == 0) vKey = 0x10;
            }
            else if (vKey == 0x11) // VK_CONTROL
            {
                vKey = (ushort)((flags & Win32Native.RI_KEY_E0) != 0 ? 0xA3 : 0xA2); // VK_RCONTROL : VK_LCONTROL
            }
            else if (vKey == 0x12) // VK_MENU
            {
                vKey = (ushort)((flags & Win32Native.RI_KEY_E0) != 0 ? 0xA5 : 0xA4); // VK_RMENU : VK_LMENU
            }

            bool isBreak = (flags & Win32Native.RI_KEY_BREAK) != 0;
            bool isMake = !isBreak;

            HandleKeyEvent(vKey, isMake);
        }

        public void HandleKeyEvent(int vk, bool isDown) => HandleKeyEvent((ushort)vk, isDown);

        public void HandleKeyEvent(ushort vKey, bool isMake)
        {
            if (_multiBindings.TryGetValue(vKey, out var buttons) && buttons.Count > 0)
            {
                if (isMake)
                {
                    _pressedPhysicalKeys.Add(vKey);
                }
                else
                {
                    _pressedPhysicalKeys.Remove(vKey);
                }

                // Iterate through all bound buttons completely without early return or break
                foreach (var button in buttons)
                {
                    if (isMake)
                    {
                        _rawInputState.SetButton(button, true);
                    }
                    else
                    {
                        bool stillPressed = false;
                        foreach (int pk in _pressedPhysicalKeys)
                        {
                            if (_multiBindings.TryGetValue(pk, out var bList) && bList.Contains(button))
                            {
                                stillPressed = true;
                                break;
                            }
                        }
                        _rawInputState.SetButton(button, stillPressed);
                    }
                }

                EmitCurrentPacket(force: false);
            }
            else
            {
                if (!isMake)
                {
                    _pressedPhysicalKeys.Remove(vKey);
                }
            }
        }

        private void EmitCurrentPacket(bool force)
        {
            InputState cleanedState = _rawInputState;
            _socdCleaner.Clean(ref cleanedState);

            ReadOnlySpan<byte> packet = _packetBuilder.BuildPacket(in cleanedState, _modeOn, _guideLocked);
            ReadOnlySpan<byte> payload = packet.Slice(0, 5);

            if (force || !payload.SequenceEqual(_lastPayload))
            {
                payload.CopyTo(_lastPayload);
                _lastSendTimestamp = Stopwatch.GetTimestamp();
                OnPacketGenerated?.Invoke(packet, cleanedState);
            }
        }

        [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
        private static extern uint MapVirtualKeyEx(uint uCode, uint uMapType);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Stop();

            if (_rawInputBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_rawInputBuffer);
                _rawInputBuffer = IntPtr.Zero;
            }

            GC.SuppressFinalize(this);
        }
    }
}

