# 📹 Samsung VG-STC4000 TV Camera Windows Driver & Auto-Loader

<div align="center">

# 🌟 Developed by **Saif Ul Islam** (`saifulislamj`)
### 🔗 **GitHub Profile:** [https://github.com/saifulislamj](https://github.com/saifulislamj)

---

### **Official 1-Click Installer • Fully Automated • Auto Plug-and-Play • Full HD Video & Microphone**
Supports **Windows 10** and **Windows 11** (64-bit)

</div>

---

## 💡 Background & The Problem

The **Samsung VG-STC4000** was an expensive, high-quality TV camera made by Samsung for Skype video calls on Smart TVs. When Skype was discontinued on TVs, these cameras were abandoned and labeled "paperweights" because:
1. **No onboard flash memory:** The camera only has a low-level bootloader; its operational firmware lives strictly in temporary RAM.
2. **Missing TV bootloader:** On Samsung TVs, the TV pushed the firmware into the camera over USB every time it turned on.
3. **Useless on Windows out of the box:** Plugging it into Windows leaves it uninitialized or giving error codes when opened.

This project delivers a **100% automated, 1-Click Windows solution** that turns the Samsung VG-STC4000 into a fully functional, plug-and-play webcam and microphone on modern Windows PCs!

---

## ⚙️ Recommended Camera Configuration (Settings)

In the **Windows Camera App** (or your meeting software), open **Settings** (<kbd>⚙️</kbd>) and apply these optimal settings:

| Setting | Recommended Value | Why |
| :--- | :--- | :--- |
| **Flicker reduction** | **60 Hz** | Eliminates horizontal banding / rolling bars under indoor lighting. |
| **Video Quality** | **720p 16:9 @ 30fps** | Provides butter-smooth 30fps motion with zero stutter or lag. |
| **Photo Quality** | **0.9MP 16:9 (1280x720)** | Matches the native 1:1 sensor grid without digital scaling. |
| **Microphone** | **USB Audio Device** | Native stereo array provides clear voice capture and noise reduction. |
| **Lighting** | **Normal Room Light** | Allows the camera shutter to run at full speed without motion blur. |

---

## 🚀 How to Install (1-Click)

1. **Plug** your Samsung VG-STC4000 into any USB port on your PC (preferably a blue USB 3.0 rear port).
2. Download this repository (click **Code** → **Download ZIP** and extract it).
3. Double-click **`Setup.exe`**.
4. Windows will show the standard Administrator prompt (UAC) → click **Yes**.

### What `Setup.exe` does automatically in 5 seconds:
* **[1/4]** Installs the pre-signed driver security certificate into Windows.
* **[2/4]** Registers the Samsung camera WinUSB driver into Windows via `pnputil`.
* **[3/4]** Streams all 21,335 firmware chunks into the camera RAM and boots the sensor.
* **[4/4]** Sets up the silent background Plug-and-Play watcher in Windows Startup.

**Done! Open your Windows Camera app, Zoom, Google Meet, Microsoft Teams, or OBS — your camera is live!**

---

## 🔄 Plug-and-Play & Unplug/Replug Behavior

Because the camera stores its firmware entirely in **volatile RAM**, every time you physically unplug the USB cable or restart your PC:
1. The camera powers off and resets into bootloader mode.
2. When you plug it back in, the **background watcher** automatically detects the connection.
3. In **~5 to 7 seconds**, it streams the firmware into RAM and boots the camera.
4. Windows will play a **notification chime** once the camera is active (`SAMSUNG VD USB HDCAM`).

> [!TIP]
> **Windows Camera App Note:** If your Windows Camera app was open while you unplugged the webcam, the Camera app will show a connection error (`0xA00F4271`). Once you plug it back in and hear the chime, simply **close and re-open the Camera app** to resume video streaming!

---

## 🎙️ Enabling the Built-in Stereo Microphone

Your Samsung VG-STC4000 also contains a **high-quality dual-microphone array**:

1. Press <kbd>Win</kbd> + <kbd>X</kbd> and select **Device Manager**.
2. Under **Other devices**, right-click **`USB2.0 UVC HQ WebCam`** (the one with `MI_03` in Hardware IDs).
3. Click **Update driver** → **Browse my computer for drivers** → **Let me pick from a list of available drivers on my computer**.
4. Select **Sound, video and game controllers** (or *Audio inputs and outputs*).
5. Choose **Microsoft** → **USB Audio Device**.
6. Click **Next** → **Yes**.

Your microphone is now fully active for crystal-clear voice recording!

---

## 📦 Package Contents

* **`Setup.exe`** — The standalone, official 1-Click installer with embedded camera icon & UAC elevation.
* **`camera.ico`** — Custom camera application icon.
* **`SamsungCamLoader.exe`** — Fast C# firmware streaming engine with dynamic GUID detection.
* **`FalconPlus_FW.bin`** — The camera firmware binary.
* **`StartCameraWatcher.vbs`** — Background silent watcher for plug-and-play.
* **`driver/`** — Pre-signed WinUSB driver package (`.inf`, `.cat`, `.cer`).
* **`Install.bat`** / **`Uninstall.bat`** — Optional command-line scripts.
* **`build.bat`** — Rebuild all binaries from source using Windows' built-in .NET compiler.

---

## 👏 Special Thanks & Acknowledgements

* **Original Linux Research & Firmware Extraction:**  
  A huge special thanks to **[Ondrej Zary](https://github.com/ondrej-zary)** for his groundbreaking work reverse engineering the Samsung TV camera protocols on Linux:  
  👉 **[ondrej-zary/samsung-tvcam-fwloader on GitHub](https://github.com/ondrej-zary/samsung-tvcam-fwloader)**

* **Windows 1-Click Suite Developer:**  
  **Saif Ul Islam**  
  GitHub: [@saifulislamj](https://github.com/saifulislamj)  
  Repository: [https://github.com/saifulislamj](https://github.com/saifulislamj)

---

## 📜 License

This project is open-source under the [MIT License](LICENSE).  
Copyright (c) 2026 **Saif Ul Islam (saifulislamj)**.
