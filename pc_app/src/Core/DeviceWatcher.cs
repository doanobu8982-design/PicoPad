using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace GamepadEmulator.Core
{
    public enum PicoDeviceMode
    {
        Disconnected = 0,
        ModeA, // PID 0x000A (Stealth / Normal Mode CDC)
        ModeB  // PID 0x000B (Game Mode: XInput + CDC)
    }

    public record PicoDeviceInfo(PicoDeviceMode Mode, string? PortName);

    public class DeviceWatcher : IDisposable
    {
        private const string VidPattern = "VID_2E8A";
        private const string PidModeAPattern = "PID_000A";
        private const string PidModeBPattern = "PID_000B";

        private readonly object _syncLock = new();
        private Timer? _pollingTimer;
        private PicoDeviceInfo _currentInfo = new(PicoDeviceMode.Disconnected, null);
        private bool _disposed;

        public event Action<PicoDeviceInfo>? DeviceStatusChanged;

        public PicoDeviceInfo CurrentInfo
        {
            get
            {
                lock (_syncLock)
                {
                    return _currentInfo;
                }
            }
        }

        public void Start()
        {
            // Initial scan
            TriggerScan();

            // Background polling fallback every 1.5s to ensure auto-recovery
            _pollingTimer = new Timer(_ => TriggerScan(), null, 1500, 1500);
        }

        public void OnDeviceChangeReceived(uint wParam)
        {
            if (wParam == Win32Native.DBT_DEVICEARRIVAL || wParam == Win32Native.DBT_DEVICEREMOVECOMPLETE)
            {
                // Delay slightly (150ms) to allow Windows PnP and usbser.sys to register the port
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    Thread.Sleep(150);
                    TriggerScan();
                });
            }
        }

        public void TriggerScan()
        {
            PicoDeviceInfo detected = ScanForPicoDevice();
            bool changed = false;

            lock (_syncLock)
            {
                if (_currentInfo != detected)
                {
                    _currentInfo = detected;
                    changed = true;
                }
            }

            if (changed)
            {
                DeviceStatusChanged?.Invoke(detected);
            }
        }

        private PicoDeviceInfo ScanForPicoDevice()
        {
            try
            {
                using RegistryKey? usbKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB");
                if (usbKey == null) return new PicoDeviceInfo(PicoDeviceMode.Disconnected, null);

                string[] subKeys = usbKey.GetSubKeyNames();

                // Check for Mode B first (Game mode)
                foreach (string keyName in subKeys)
                {
                    if (keyName.IndexOf(VidPattern, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        keyName.IndexOf(PidModeBPattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string? port = FindPortNameInDevice(usbKey, keyName);
                        if (!string.IsNullOrEmpty(port))
                        {
                            return new PicoDeviceInfo(PicoDeviceMode.ModeB, port);
                        }
                    }
                }

                // Check for Mode A (Normal / Stealth CDC)
                foreach (string keyName in subKeys)
                {
                    if (keyName.IndexOf(VidPattern, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        keyName.IndexOf(PidModeAPattern, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string? port = FindPortNameInDevice(usbKey, keyName);
                        if (!string.IsNullOrEmpty(port))
                        {
                            return new PicoDeviceInfo(PicoDeviceMode.ModeA, port);
                        }
                    }
                }
            }
            catch
            {
                // Ignore registry access exceptions
            }

            return new PicoDeviceInfo(PicoDeviceMode.Disconnected, null);
        }

        private const int Win32ErrorSuccess = 0;
        private const int Win32ErrorAccessDenied = 5;

        private static bool IsPortActive(string portName)
        {
            if (string.IsNullOrWhiteSpace(portName)) return false;

            try
            {
                string devicePath = @"\\.\" + portName;
                IntPtr hFile = Win32Native.CreateFile(
                    devicePath,
                    Win32Native.GENERIC_READ | Win32Native.GENERIC_WRITE,
                    0,
                    IntPtr.Zero,
                    Win32Native.OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (hFile == Win32Native.INVALID_HANDLE_VALUE)
                {
                    int error = Marshal.GetLastWin32Error();
                    return error == Win32ErrorAccessDenied || error == Win32ErrorSuccess;
                }

                Win32Native.CloseHandle(hFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string? FindPortNameInDevice(RegistryKey parentKey, string deviceSubKey)
        {
            using RegistryKey? devKey = parentKey.OpenSubKey(deviceSubKey);
            if (devKey == null) return null;

            string[] instanceNames = devKey.GetSubKeyNames();
            foreach (string instance in instanceNames)
            {
                using RegistryKey? instKey = devKey.OpenSubKey(instance);
                if (instKey == null) continue;

                // Check "Device Parameters"
                using RegistryKey? paramKey = instKey.OpenSubKey("Device Parameters");
                if (paramKey != null)
                {
                    object? portVal = paramKey.GetValue("PortName");
                    if (portVal is string portName && portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                    {
                        if (IsPortActive(portName))
                        {
                            return portName;
                        }
                    }
                }
            }

            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _pollingTimer?.Dispose();
            _pollingTimer = null;
            GC.SuppressFinalize(this);
        }
    }
}

