using System;
using System.Runtime.InteropServices;

namespace GamepadEmulator.Core
{
    public class SerialCommunicator : IDisposable
    {
        private readonly object _syncLock = new();
        private IntPtr _hPort = IntPtr.Zero;
        private string? _currentPort;
        private bool _disposed;

        public event Action<string>? Connected;
        public event Action? Disconnected;

        public bool IsConnected
        {
            get
            {
                lock (_syncLock)
                {
                    return _hPort != IntPtr.Zero && _hPort != Win32Native.INVALID_HANDLE_VALUE;
                }
            }
        }

        public string? CurrentPort
        {
            get
            {
                lock (_syncLock)
                {
                    return _currentPort;
                }
            }
        }

        public bool Connect(string portName)
        {
            lock (_syncLock)
            {
                if (IsConnected)
                {
                    if (string.Equals(_currentPort, portName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    DisconnectInternal();
                }

                string formattedPath = @"\\.\" + portName;
                IntPtr handle = Win32Native.CreateFile(
                    formattedPath,
                    Win32Native.GENERIC_READ | Win32Native.GENERIC_WRITE,
                    0,
                    IntPtr.Zero,
                    Win32Native.OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (handle == Win32Native.INVALID_HANDLE_VALUE || handle == IntPtr.Zero)
                {
                    return false;
                }

                // Explicitly assert DTR High immediately to ensure Pico's safety net initializes
                Win32Native.EscapeCommFunction(handle, Win32Native.SETDTR);

                // Configure safe 50ms write timeout
                Win32Native.COMMTIMEOUTS timeouts = new()
                {
                    ReadIntervalTimeout = 0,
                    ReadTotalTimeoutMultiplier = 0,
                    ReadTotalTimeoutConstant = 0,
                    WriteTotalTimeoutMultiplier = 0,
                    WriteTotalTimeoutConstant = 50
                };
                Win32Native.SetCommTimeouts(handle, ref timeouts);

                _hPort = handle;
                _currentPort = portName;
            }

            Connected?.Invoke(portName);
            return true;
        }

        public void Disconnect()
        {
            bool wasConnected;
            lock (_syncLock)
            {
                wasConnected = IsConnected;
                DisconnectInternal();
            }

            if (wasConnected)
            {
                Disconnected?.Invoke();
            }
        }

        private void DisconnectInternal()
        {
            if (_hPort != IntPtr.Zero && _hPort != Win32Native.INVALID_HANDLE_VALUE)
            {
                Win32Native.CloseHandle(_hPort);
                _hPort = IntPtr.Zero;
            }
            _currentPort = null;
        }

        public unsafe bool SendPacket(ReadOnlySpan<byte> packet)
        {
            if (packet.Length != 6) return false;

            lock (_syncLock)
            {
                if (!IsConnected) return false;

                fixed (byte* pBuf = packet)
                {
                    bool success = Win32Native.WriteFile(
                        _hPort,
                        pBuf,
                        6,
                        out uint bytesWritten,
                        IntPtr.Zero);

                    if (!success || bytesWritten != 6)
                    {
                        // Transmission failed or timed out: device disconnected
                        DisconnectInternal();
                        // Invoke outside lock
                        ThreadPoolQueueDisconnected();
                        return false;
                    }
                }

                return true;
            }
        }

        public unsafe bool SendCommand(byte cmdId)
        {
            byte[] cmdBuf = new byte[6];
            cmdBuf[0] = PacketBuilder.CommandHeader; // 0xA5
            cmdBuf[1] = cmdId;
            cmdBuf[2] = (byte)(~cmdId);
            cmdBuf[3] = 0x00;
            cmdBuf[4] = 0x00;
            cmdBuf[5] = PacketBuilder.ComputeCrc8(cmdBuf.AsSpan(0, 5));

            lock (_syncLock)
            {
                if (!IsConnected) return false;

                fixed (byte* pBuf = cmdBuf)
                {
                    bool success = Win32Native.WriteFile(
                        _hPort,
                        pBuf,
                        6,
                        out uint bytesWritten,
                        IntPtr.Zero);

                    if (success && bytesWritten == 6)
                    {
                        // Guarantee hardware synchronous flush before closing handle
                        Win32Native.FlushFileBuffers(_hPort);
                    }
                }

                // Close port cleanly to allow Pico reboot & host re-enumeration
                DisconnectInternal();
            }

            Disconnected?.Invoke();
            return true;
        }

        private void ThreadPoolQueueDisconnected()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                Disconnected?.Invoke();
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Disconnect();
            GC.SuppressFinalize(this);
        }
    }
}

