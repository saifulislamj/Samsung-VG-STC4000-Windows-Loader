using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

class SamsungCamLoader
{
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_OVERLAPPED = 0x40000000;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WINUSB_SETUP_PACKET
    {
        public byte RequestType;
        public byte Request;
        public ushort Value;
        public ushort Index;
        public ushort Length;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("winusb.dll", SetLastError = true)]
    private static extern bool WinUsb_Initialize(IntPtr DeviceHandle, out IntPtr InterfaceHandle);

    [DllImport("winusb.dll", SetLastError = true)]
    private static extern bool WinUsb_Free(IntPtr InterfaceHandle);

    [DllImport("winusb.dll", SetLastError = true)]
    private static extern bool WinUsb_SetPipePolicy(
        IntPtr InterfaceHandle, byte PipeID, uint PolicyType, uint ValueLength, ref uint Value);

    [DllImport("winusb.dll", SetLastError = true)]
    private static extern bool WinUsb_ControlTransfer(
        IntPtr InterfaceHandle, WINUSB_SETUP_PACKET SetupPacket,
        byte[] Buffer, uint BufferLength, out uint LengthTransferred, IntPtr Overlapped);

    private static string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "watcher.log");

    public static void Log(string message)
    {
        string timestamped = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}", DateTime.Now, message);
        Console.WriteLine(timestamped);
        try
        {
            File.AppendAllText(logFilePath, timestamped + Environment.NewLine);
        }
        catch { }
    }

    public static bool IsCameraActive()
    {
        try
        {
            using (RegistryKey usbKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceClasses\{a5dcbf10-6530-11d2-901f-00c04fb951ed}"))
            {
                if (usbKey != null)
                {
                    foreach (string sub in usbKey.GetSubKeyNames())
                    {
                        if (sub.IndexOf("VID_04E8&PID_2062", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string formatted = sub.StartsWith("##?#") ? @"\\?\" + sub.Substring(4) : sub;
                            IntPtr h = CreateFile(formatted, GENERIC_READ | GENERIC_WRITE,
                                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                            if (h != (IntPtr)(-1))
                            {
                                CloseHandle(h);
                                return true;
                            }
                        }
                    }
                }
            }
        }
        catch { }
        return false;
    }

    public static string FindUnfirmwaredCameraPath()
    {
        // 1. Check known interface GUIDs first (fast)
        string[] guids = new string[] {
            "{cf80a6e7-97e3-46a6-9a90-67929603c3ff}",
            "{55892826-bb5d-477d-90ff-a734fac8be74}",
            "{dee824ef-729b-4a0e-9c14-b7117d33a817}"
        };

        foreach (string g in guids)
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceClasses\" + g))
                {
                    if (k != null)
                    {
                        foreach (string sub in k.GetSubKeyNames())
                        {
                            if (sub.IndexOf("VID_04E8&PID_2061&MI_02", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                string formatted = sub.StartsWith("##?#") ? @"\\?\" + sub.Substring(4) : sub;
                                IntPtr h = CreateFile(formatted, GENERIC_READ | GENERIC_WRITE,
                                    FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                                if (h != (IntPtr)(-1))
                                {
                                    CloseHandle(h);
                                    return formatted;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        // 2. Comprehensive fallback scan
        try
        {
            using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceClasses"))
            {
                if (baseKey != null)
                {
                    foreach (string g in baseKey.GetSubKeyNames())
                    {
                        using (RegistryKey k = baseKey.OpenSubKey(g))
                        {
                            if (k == null) continue;
                            foreach (string sub in k.GetSubKeyNames())
                            {
                                if (sub.IndexOf("VID_04E8&PID_2061&MI_02", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    string formatted = sub.StartsWith("##?#") ? @"\\?\" + sub.Substring(4) : sub;
                                    IntPtr h = CreateFile(formatted, GENERIC_READ | GENERIC_WRITE,
                                        FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                                    if (h != (IntPtr)(-1))
                                    {
                                        CloseHandle(h);
                                        return formatted;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }

        return null;
    }

    const ushort AIT_IDX = 0x0600;
    const ushort AIT_CMD_UPLOAD = 0x03;
    const ushort AIT_CMD_FW = 0x04;
    const int AIT_FW_CHUNK_SIZE = 32;
    const uint PIPE_TRANSFER_TIMEOUT = 0x03;

    static bool SendAitCmd(IntPtr winUsbHandle, byte[] data, ushort val, bool outDir)
    {
        WINUSB_SETUP_PACKET pkt = new WINUSB_SETUP_PACKET();
        pkt.RequestType = (byte)(0x20 | (outDir ? 0x00 : 0x80)); // Exactly LIBUSB_REQUEST_TYPE_CLASS
        pkt.Request = 0x01; // UVC_SET_CUR
        pkt.Value = (ushort)(val << 8);
        pkt.Index = AIT_IDX;
        pkt.Length = (ushort)(data != null ? data.Length : 0);

        uint transferred;
        return WinUsb_ControlTransfer(winUsbHandle, pkt, data, pkt.Length, out transferred, IntPtr.Zero);
    }

    public static bool LoadFirmware(string devPath = null)
    {
        if (string.IsNullOrEmpty(devPath))
        {
            devPath = FindUnfirmwaredCameraPath();
        }

        if (devPath == null)
        {
            if (IsCameraActive())
            {
                Log("[+] Camera is already active with firmware booted (PID 2062).");
                return true;
            }
            Log("[-] Camera hardware not detected. Please ensure Samsung VG-STC4000 is plugged in.");
            return false;
        }

        Log("[+] Connecting to camera interface: " + devPath);

        IntPtr hFile = CreateFile(devPath,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_OVERLAPPED,
            IntPtr.Zero);

        if (hFile == (IntPtr)(-1))
        {
            Log("[-] Failed to open device handle. Error: " + Marshal.GetLastWin32Error());
            return false;
        }

        try
        {
            IntPtr winUsb;
            if (!WinUsb_Initialize(hFile, out winUsb))
            {
                Log("[-] WinUsb_Initialize failed. Error: " + Marshal.GetLastWin32Error());
                return false;
            }

            try
            {
                uint timeoutMs = 3000;
                WinUsb_SetPipePolicy(winUsb, 0x00, PIPE_TRANSFER_TIMEOUT, 4, ref timeoutMs);

                string fwPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FalconPlus_FW.bin");
                if (!File.Exists(fwPath))
                    fwPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samsung-tvcam-fwloader-main", "FalconPlus_FW.bin");
                if (!File.Exists(fwPath))
                    fwPath = @"C:\Users\John\Downloads\Compressed\samsung-tvcam-fwloader-main\FalconPlus_FW.bin";

                if (!File.Exists(fwPath))
                {
                    Log("[-] Firmware file not found: FalconPlus_FW.bin");
                    return false;
                }

                byte[] fwBytes = File.ReadAllBytes(fwPath);
                Log("[+] Loaded firmware binary: " + fwBytes.Length + " bytes");

                // Step 1: Start FW upload
                Log("[*] Initializing firmware transfer sequence...");
                byte[] initCmd = new byte[8] { 0x01, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00 };
                if (!SendAitCmd(winUsb, initCmd, AIT_CMD_FW, true))
                {
                    Log("[-] Failed to send FW start command. Error: " + Marshal.GetLastWin32Error());
                    return false;
                }

                // Step 2: Upload chunks
                int totalChunks = (fwBytes.Length + AIT_FW_CHUNK_SIZE - 1) / AIT_FW_CHUNK_SIZE;
                Log("[*] Streaming " + totalChunks + " chunks into camera RAM...");

                for (int i = 0; i < fwBytes.Length; i += AIT_FW_CHUNK_SIZE)
                {
                    byte[] chunk = new byte[AIT_FW_CHUNK_SIZE];
                    int len = Math.Min(AIT_FW_CHUNK_SIZE, fwBytes.Length - i);
                    Array.Copy(fwBytes, i, chunk, 0, len);

                    if (!SendAitCmd(winUsb, chunk, AIT_CMD_UPLOAD, true))
                    {
                        Log("[-] Failed uploading chunk at offset " + i + ". Error: " + Marshal.GetLastWin32Error());
                        return false;
                    }

                    int currentChunk = (i / AIT_FW_CHUNK_SIZE) + 1;
                    if (currentChunk % 5000 == 0 || currentChunk == totalChunks)
                    {
                        int pct = (int)((currentChunk / (double)totalChunks) * 100);
                        Log(string.Format("    [{0}%] Uploaded {1}/{2} chunks", pct, currentChunk, totalChunks));
                    }
                }

                Log("[+] All firmware chunks uploaded successfully!");

                // Step 3: Boot uploaded firmware
                Log("[*] Sending Boot Firmware command...");
                initCmd[1] = 0x01;
                if (!SendAitCmd(winUsb, initCmd, AIT_CMD_FW, true))
                {
                    Log("[-] Failed to send Boot command. Error: " + Marshal.GetLastWin32Error());
                    return false;
                }

                Log("[***] SUCCESS! Firmware booted successfully! Camera is now active!");
                return true;
            }
            finally
            {
                WinUsb_Free(winUsb);
            }
        }
        finally
        {
            CloseHandle(hFile);
        }
    }

    public static void RunWatcher()
    {
        Log("=================================================");
        Log("   Samsung VG-STC4000 Plug-and-Play Watcher     ");
        Log("=================================================");
        Log("[*] Monitoring USB ports. Camera will auto-initialize whenever plugged in.");

        bool wasActive = IsCameraActive();
        if (wasActive)
        {
            Log("[+] Camera is currently active (SAMSUNG VD USB HDCAM).");
        }

        while (true)
        {
            try
            {
                bool active = IsCameraActive();
                if (active)
                {
                    if (!wasActive)
                    {
                        Log("[+] Samsung Camera is active and ready (SAMSUNG VD USB HDCAM)!");
                        wasActive = true;
                    }
                }
                else
                {
                    // Check if camera was connected in raw bootloader state (PID 2061)
                    string rawPath = FindUnfirmwaredCameraPath();
                    if (rawPath != null)
                    {
                        Log("[+] Samsung Camera plugged in (VID_04E8&PID_2061).");
                        Log("[*] Initializing firmware upload...");

                        bool success = false;
                        for (int attempt = 1; attempt <= 5; attempt++)
                        {
                            Thread.Sleep(400 * attempt);
                            string path = FindUnfirmwaredCameraPath();
                            if (path != null && LoadFirmware(path))
                            {
                                success = true;
                                break;
                            }
                            Log(string.Format("[!] Upload attempt {0} failed. Retrying...", attempt));
                            Thread.Sleep(800);
                        }

                        if (success)
                        {
                            Log("[***] Camera initialized successfully! Device is ready.");
                            wasActive = true;
                            try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
                        }
                        else
                        {
                            Log("[-] Failed to initialize camera after 5 attempts.");
                        }
                    }
                    else
                    {
                        if (wasActive)
                        {
                            Log("[-] Samsung Camera was unplugged.");
                            wasActive = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("[!] Watcher error: " + ex.Message);
            }

            Thread.Sleep(1000);
        }
    }

    public static int Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("   Samsung VG-STC4000 TV Camera Windows Loader   ");
        Console.WriteLine("=================================================");

        if (args.Length > 0 && (args[0] == "-w" || args[0] == "--watch" || args[0] == "/watch"))
        {
            RunWatcher();
            return 0;
        }

        bool ok = LoadFirmware();
        return ok ? 0 : 1;
    }
}
