# ✨ Yui's Gaming Overlay ✨

[![Made with C#](https://img.shields.io/badge/Made_with-C%23-purple?style=for-the-badge&logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Windows Only](https://img.shields.io/badge/OS-Windows-blue?style=for-the-badge&logo=windows)](https://www.microsoft.com/windows)
[![Vibes](https://img.shields.io/badge/Vibes-Immaculate-pink?style=for-the-badge)](#)

Hiiii! 👋 Welcome to my gaming overlay! 

So, I literally *could not* find a lightweight overlay that did exactly what I wanted without eating up all my RAM, so I just... made my own? It's written in C# and it sits transparently over your games to give you hardware stats, a kill counter, a speedrunning stopwatch, and my favorite part, the NOTES!!

It uses raw Windows API hooks so it reads your keypresses perfectly even when you're tabbed into a game. It also doesn't trigger anticheat thanks to the help of HwiNFO! :)

---

## 💖 The Tea

*   📝 **YuiNotes (Uh totally not just notes yep):** Literally just type notes on your screen! You can toggle Edit Mode, drag them around with arrow keys, and leave yourself reminders
*   ⚔️ **Kill/Death Counter:** Easily track how many times you've died to a boss or how many kills you're at. 
*   ⏱️ **Speedrunner Stopwatch:** A full stopwatch with lap times! It even prints your laps directly to the screen and your console.
*   💻 **Hardware Monitoring:** Hooks directly into HWiNFO's C++ shared memory to show your CPU/GPU temps and usage, plus your FPS!
*   🎯 **Custom Crosshair:** Puts a little red dot right in the center of your screen!
*   🕰️ **System Clock:** Because sometimes you play the Sims for 8 hours and forget what year it is.
*   🚨 **PANIC BUTTON:** Press F12 and your entire screen instantly goes black. YOU'RE WELCOME!
*   📺 **Resolution Independent:** Dynamically scales fonts and UI based on your monitor's resolution. 1080p, 1440p, 4K?

---

## 🎮 Keyboard Controls

Everything is controlled via hotkeys so you don't have to Alt+Tab. *Make sure your Numpad is ON!* 

### 📝 YuiNotes
| Key | What it does |
| :--- | :--- |
| `Page Down` | Toggle YuiNotes visibility |
| `Page Up` | Toggle **Edit Mode** ✨ (Must be ON to type/move!) |
| `Arrow Keys` | Move the note around the screen *(Requires Edit Mode)* |
| `A-Z, Space, Enter, Backspace` | Type your note! *(Requires Edit Mode)* |
| `Ctrl + F11` | Wipe everything and reset to a single period |

### ⚔️ Kill Counter
| Key | What it does |
| :--- | :--- |
| `F8` | Show/Hide the Counter |
| `Numpad +` | Add 1 |
| `Numpad -` | Subtract 1 |
| `F9` | Reset to 0 |

### ⏱️ Stopwatch
| Key | What it does |
| :--- | :--- |
| `Numpad 1` | Start / Pause |
| `Numpad 2` | Save Lap Time (shows on screen + console!) |
| `Numpad 3` | Reset Stopwatch & Lap |
| `Numpad 4` | Show/Hide Stopwatch |

### 💻 Hardware Stats
| Key | What it does |
| :--- | :--- |
| `Numpad 5` | Toggle CPU Temp 🌡️ |
| `Numpad 9` | Toggle CPU Load ⚙️ |
| `Numpad 6` | Toggle GPU Temp 🌡️ |
| `Numpad 7` | Toggle GPU Load ⚙️ |
| `Numpad 8` | Toggle FPS (needs RTSS running!) 🏃‍♀️ |

### 🎀 Extras
| Key | What it does |
| :--- | :--- |
| `Numpad 0` | Toggle Center Crosshair 🔴 |
| `F11` | Toggle System Clock 🕰️ |
| `F12` | **PANIC BUTTON** (Blacks out the screen, God save you) ⬛ |

---

## 🛠️ What You Need (Prerequisites)
Okay, so to make the magic happen, especially the PC temperature stuff, ya need a little bit of setup:

1. **Windows 10/11**: Obviously. It uses `user32.dll` and `gdi32.dll`.
2. **[HWiNFO64](https://www.hwinfo.com/)**: This is REQUIRED if you want CPU/GPU stats! 
   * *Important:* You need to go into HWiNFO settings and enable **Shared Memory Support**. Otherwise, my app can't read the data! 😭
3. **[RivaTuner Statistics Server (RTSS)](https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html)**: Optional, but you need this running in the background if you want the FPS counter to work.

## 🚀 How to Run
1. Download the release.
2. Run the release!
3. Enjoy!

## 💌 Contributing
If you wanna add more features, feel free to submit a pull request! Please just don't make the code messy, I tried really hard to keep it organized :D.

Okay bye, have fun gaming!! <3
