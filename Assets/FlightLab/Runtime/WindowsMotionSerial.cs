using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FlightLab
{
    // Small Windows serial transport, so Unity's .NET Standard profile needs no extra DLLs.
    // Only the worker thread owns this object; reads always time out within 50 ms.
    internal sealed class WindowsMotionSerial : IDisposable
    {
        private SafeFileHandle handle;
        public WindowsMotionSerial(string port, int baud)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(port, @"^COM[1-9][0-9]*$"))
                throw new ArgumentException("Use a Windows port name such as COM3.");
            handle = CreateFile(@"\\.\" + port, 0xC0000000, 0, IntPtr.Zero, 3, 0, IntPtr.Zero);
            try
            {
                if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
                Dcb config = new Dcb { Length = (uint)Marshal.SizeOf(typeof(Dcb)) };
                Check(GetCommState(handle, ref config));
                config.BaudRate = (uint)baud; config.Flags = 1; // Binary, no flow control, DTR/RTS off.
                config.ByteSize = 8; config.Parity = 0; config.StopBits = 0;
                Check(SetCommState(handle, ref config));
                Timeouts timeouts = new Timeouts { ReadInterval = uint.MaxValue, ReadMultiplier = uint.MaxValue,
                    ReadConstant = 50, WriteConstant = 100 };
                Check(SetCommTimeouts(handle, ref timeouts));
                Check(SetupComm(handle, 8192, 1024));
                Check(PurgeComm(handle, 8));
            }
            catch { Dispose(); throw; }
        }
        public int Read(byte[] bytes)
        {
            uint read;
            Check(ReadFile(handle, bytes, (uint)bytes.Length, out read, IntPtr.Zero));
            return (int)read;
        }
        public void Dispose() { handle?.Dispose(); }
        private static void Check(bool ok) { if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error()); }
        [StructLayout(LayoutKind.Sequential)] private struct Dcb
        {
            public uint Length, BaudRate, Flags;
            public ushort Reserved, XonLimit, XoffLimit;
            public byte ByteSize, Parity, StopBits;
            public sbyte XonChar, XoffChar, ErrorChar, EofChar, EventChar;
            public ushort Reserved1;
        }
        [StructLayout(LayoutKind.Sequential)] private struct Timeouts
        { public uint ReadInterval, ReadMultiplier, ReadConstant, WriteMultiplier, WriteConstant; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetCommState(SafeFileHandle h, ref Dcb dcb);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetCommState(SafeFileHandle h, ref Dcb dcb);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetCommTimeouts(SafeFileHandle h, ref Timeouts timeouts);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetupComm(SafeFileHandle h, uint input, uint output);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool PurgeComm(SafeFileHandle h, uint flags);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadFile(SafeFileHandle h, byte[] buffer, uint count, out uint read, IntPtr overlapped);
    }
}
