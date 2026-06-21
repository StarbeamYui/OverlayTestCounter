using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using GameOverlay.Drawing;
using GameOverlay.Windows;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace OverlayTestCounter
{
    class Program
    {
        // ====================================================================
        // WINDOWS API IMPORTS (P/Invoke)
        // These are required to interact with the OS at a low level, allowing
        // us to read keystrokes even when this console app isn't in focus, 
        // and to get accurate monitor information.
        // ====================================================================

        // Retrieves the status of a specific virtual key. Used for global hotkeys.
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        // Retrieves system metrics or configuration settings. Used to get screen dimensions.
        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        // Retrieves a handle to a device context (DC) for the client area of a specified window.
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        // Releases the device context obtained by GetDC.
        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hDC, IntPtr hWnd);

        // Retrieves device-specific information for the specified device. Used here to get monitor refresh rate.
        [DllImport("gdi32.dll")]
        public static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        // Constants used with the Windows APIs above
        const int VREFRESH = 116;    // Index to get vertical refresh rate
        const int SM_CXSCREEN = 0;   // Index to get primary monitor width
        const int SM_CYSCREEN = 1;   // Index to get primary monitor height

        // --- Virtual Key Codes (Hexadecimal values mapped to physical keys) ---
        // Main overlay controls
        const int VK_F8 = 0x77;
        const int VK_F9 = 0x78;
        const int VK_ADD = 0x6B;       // Numpad +
        const int VK_SUBTRACT = 0x6D;  // Numpad -
        const int VK_F10 = 0x79;


        // Stopwatch controls
        const int VK_NUMPAD1 = 0x61;
        const int VK_NUMPAD2 = 0x62;
        const int VK_NUMPAD3 = 0x63;
        const int VK_NUMPAD4 = 0x64;

        // Hardware Stat Visibility Toggles
        const int VK_NUMPAD5 = 0x65;
        const int VK_NUMPAD6 = 0x66;
        const int VK_NUMPAD7 = 0x67;
        const int VK_NUMPAD8 = 0x68;
        const int VK_NUMPAD9 = 0x69;

        //Crosshair Control
        const int VK_NUMPAD0 = 0x60;

        //Panic Button
        const int VK_F12 = 0x7B;

        //Clock Button
        const int VK_F11 = 0x7A;

        // Note Controls
        const int VK_PRIOR = 0x21; // Page Up
        const int VK_NEXT = 0x22;  // Page Down
        const int VK_LEFT = 0x25;
        const int VK_UP = 0x26;
        const int VK_RIGHT = 0x27;
        const int VK_DOWN = 0x28;
        const int VK_SPACE = 0x20;
        const int VK_BACK = 0x08;
        const int VK_RETURN = 0x0D; // Enter key
        const int VK_CONTROL = 0x11;

        // ====================================================================
        // APPLICATION STATE VARIABLES
        // ====================================================================

        // Floating Notes State
        static bool _notesVisible = false;
        static bool _writingMode = false;
        static string _noteText = "Type here...";
        static float _noteX = 50f;
        static float _noteY = 500f; 

        // Note Debounce Variables
        static bool _pgUpWasPressed = false;
        static bool _pgDnWasPressed = false;
        static bool _backWasPressed = false;
        static bool _spaceWasPressed = false;
        static bool[] _keyWasPressed = new bool[256]; // Handles A-Z debouncing effortlessly
        static bool _ctrlF11WasPressed = false;

        // Kill counter state
        static int _counter = 0;
        static bool _isVisible = true;

        // Screen dimensions (populated on startup)
        static int _screenWidth;
        static int _screenHeight;

        // Stopwatch state
        static Stopwatch _stopwatch = new Stopwatch();
        static bool _stopwatchVisible = true;

        // Lap state (NEW)
        static TimeSpan _lastLapTime;
        static bool _hasLapTime = false;

        // Panic Button & Crosshair State
        static bool _panicMode = false;
        static bool _crosshairVisible = false;
        static bool _clockVisible = true;

        // Stat visibility booleans (User can toggle these via Numpad 5-9)
        static bool _cpuTempVisible = true;
        static bool _cpuLoadVisible = true;
        static bool _gpuTempVisible = true;
        static bool _gpuLoadVisible = true;
        static bool _fpsVisible = true;


        // --- Debounce Flags ---
        // These prevent a single key press from triggering an action dozens of times a second.
        // We only trigger an action when the state changes from 'unpressed' to 'pressed'.
        static bool _f8WasPressed = false;
        static bool _f9WasPressed = false;
        static bool _f11WasPressed = false;
        static bool _f12WasPressed = false;
        static bool _enterWasPressed = false;

        static bool _addWasPressed = false;
        static bool _subtractWasPressed = false;
        static bool _numpad0WasPressed = false;
        static bool _numpad1WasPressed = false;
        static bool _numpad2WasPressed = false;
        static bool _numpad3WasPressed = false;
        static bool _numpad4WasPressed = false;
        static bool _numpad5WasPressed = false;
        static bool _numpad6WasPressed = false;
        static bool _numpad7WasPressed = false;
        static bool _numpad8WasPressed = false;
        static bool _numpad9WasPressed = false;

        // ====================================================================
        // GRAPHICS RESOURCES
        // ====================================================================
        static Font _font;
        static Font _smallfont;
        static Font _stopwatchFont;
        static SolidBrush _greenBrush;
        static SolidBrush _blackBrush; // Used to create drop-shadows for text readability
        static SolidBrush _redBrush;
        static SolidBrush _blueBrush;

        // ====================================================================
        // SENSOR THREAD VARIABLES
        // ====================================================================
        static Process _hwinfoProcess;
        static Thread _sensorThread;

        // IMPORTANT: The 'volatile' keyword tells the compiler not to optimize these variables.
        // Because the background sensor thread writes to these, and the UI drawing thread reads from them,
        // 'volatile' ensures the UI thread always reads the most up-to-date value from main memory, 
        // rather than a stale cached value.
        static volatile float _cpuTemp;
        static volatile float _cpuUsage;
        static volatile float _gpuTemp;
        static volatile float _gpuUsage;
        static volatile float _fps;
        static volatile bool _hwinfoReady; // Flag to let the UI know data is safe to read

        const string _counterText = "Current Kill Count";

        // ====================================================================
        // HWiNFO SHARED MEMORY STRUCTURES
        // These structs strictly map to HWiNFO's internal C++ memory layout.
        // Pack = 1 ensures the C# compiler doesn't add padding between fields, 
        // which would misalign the memory reading.
        // ====================================================================

        // Header structure defining where to find the actual data
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct HWiNFO_SENSORS_SHARED_MEM2
        {
            public uint dwSignature; // Should always be 'HWiS' (0x53695748 in Hex). Acts as a sanity check.
            public uint dwVersion;
            public uint dwRevision;
            public long poll_time;
            public uint dwOffsetOfSensorSection; // Offset to start of sensor list
            public uint dwSizeOfSensorElement;
            public uint dwNumSensorElements;
            public uint dwOffsetOfReadingSection; // Offset to start of actual values/readings
            public uint dwSizeOfReadingElement;
            public uint dwNumReadingElements;     // Total number of readings we need to loop through
        }

        // Structure representing a single sensor reading (e.g., "CPU Core 0 Temp")
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        public struct HWiNFO_ELEMENT
        {
            public uint tReading;      // Type of reading (1 = Temp, 7 = Usage/Load, etc.)
            public uint dwSensorIndex;
            public uint dwReadingID;

            // Fixed-size strings matching C++ char arrays
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szLabelOrig; // Original name from HWiNFO
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szLabelUser; // User-renamed name (if they changed it in HWiNFO settings)
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public string szUnit;      // E.g., "°C", "%", "FPS"

            public double value;       // Current value
            public double valueMin;
            public double valueMax;
            public double valueAvg;
        }

        static void Main(string[] args)
        {
            // 1. Ensure the data provider is running and start reading it in the background
            EnsureHwinfoRunning();
            StartSensorThread();

            // 2. Configure Graphics Quality
            var gfx = new Graphics()
            {
                MeasureFPS = true,
                PerPrimitiveAntiAliasing = true,
                TextAntiAliasing = true
            };

            // 3. Get Display Info so the overlay spans the whole screen
            _screenWidth = GetSystemMetrics(SM_CXSCREEN);
            _screenHeight = GetSystemMetrics(SM_CYSCREEN);

            // Set default note position to bottom left
            _noteX = _screenWidth * 0.02f;
            _noteY = _screenHeight * 0.75f;

            // Get device context to check monitor refresh rate. 
            // Matching overlay FPS to Monitor Hz reduces tearing/flicker.
            IntPtr hdc = GetDC(IntPtr.Zero);
            int refreshRate = GetDeviceCaps(hdc, VREFRESH);
            ReleaseDC(IntPtr.Zero, hdc);

            Console.WriteLine($"Detected Display: {_screenWidth}x{_screenHeight} @ {refreshRate}Hz");

            // 4. Initialize the Overlay Window
            var window = new GraphicsWindow(0, 0, _screenWidth, _screenHeight, gfx)
            {
                FPS = refreshRate,
                IsTopmost = true,      // Keeps overlay above games/other apps
                IsVisible = true,
                Title = "OverlayV1"
            };

            // Subscribe to graphics events
            window.SetupGraphics += Window_SetupGraphics;
            window.DrawGraphics += Window_DrawGraphics;
            window.DestroyGraphics += Window_DestroyGraphics;

            // Print instructions to the console
            Console.WriteLine("Overlay is running");
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("PAGE DOWN -> Toggle YuiNotes Visibility");
            Console.WriteLine("CTRL + F11 -> Clear notes to a single period");
            Console.WriteLine("PAGE UP -> Toggle YuiNotes Edit Mode");
            Console.WriteLine("   *WHILE EDIT MODE IS ACTIVE:");
            Console.WriteLine("   *Arrow Keys -> Move the YuiNote");
            Console.WriteLine("   *A-Z, Space, Enter, Backspace -> Edit your YuiNote");
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("F8 -> Toggle Counter Visibility");
            Console.WriteLine("Numpad+ -> Add one to counter");
            Console.WriteLine("Numpad- -> Subtract one from counter");
            Console.WriteLine("F9 -> Reset Counter");
            Console.WriteLine("F11 -> Toggle System Clock");
            Console.WriteLine("F12 -> Panic Button Toggle (Blackens entire screen)");
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("NUMPAD 0 -> Toggle Crosshair");
            Console.WriteLine("NUMPAD 1 -> Start/Stop Stopwatch");
            Console.WriteLine("NUMPAD 2 -> Lap, which prints the current time to the console and screen");
            Console.WriteLine("NUMPAD 3 -> Reset Stopwatch and Lap Time");
            Console.WriteLine("NUMPAD 4 -> Toggle Stopwatch Visibility");
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("NUMPAD 5 -> Toggle CPU Temp");
            Console.WriteLine("NUMPAD 9 -> Toggle CPU Load");
            Console.WriteLine("NUMPAD 6 -> Toggle GPU Temp");
            Console.WriteLine("NUMPAD 7 -> Toggle GPU Load");
            Console.WriteLine("NUMPAD 8 -> Toggle FPS (Requires RTSS and a program using DirectX, OpenGl, or Vulkan)");
            Console.WriteLine("------------------------------------------------");
            Console.WriteLine("Waiting for HWiNFO Shared Memory...");

            // 5. Start the overlay loop (Blocking call)
            window.Create(); // Creates the transparent window
            window.Join();   // Keeps the main thread alive as long as the window is open
        }

        // Triggered once when the window is created. Used to allocate unmanaged resources (fonts/brushes).
        private static void Window_SetupGraphics(object sender, SetupGraphicsEventArgs e)
        {
            var gfx = e.Graphics;

            // Scale font size relative to screen height (3.5% of height) to support 1080p, 1440p, 4k cleanly
            float dynamicFontSize = _screenHeight * 0.035f;
            float smallDynamicFontSize = _screenHeight * 0.025f;


            _font = gfx.CreateFont("Oswald", dynamicFontSize, bold: true);
            _smallfont = gfx.CreateFont("Oswald", smallDynamicFontSize, bold: true);
            _stopwatchFont = gfx.CreateFont("Oswald", dynamicFontSize, bold: true);

            _redBrush = e.Graphics.CreateSolidBrush(255, 0, 0);
            _blueBrush = e.Graphics.CreateSolidBrush(0, 0, 255);
            _greenBrush = gfx.CreateSolidBrush(170, 229, 164); // Pale green
            _blackBrush = gfx.CreateSolidBrush(0, 0, 0);       // Used for text shadows
        }

        // Triggered every frame (e.g., 144 times a second if 144Hz)
        private static void Window_DrawGraphics(object sender, DrawGraphicsEventArgs e)
        {
            var gfx = e.Graphics;

            // --- INPUT POLLING ---
            // GetAsyncKeyState returns a short. If the most significant bit (0x8000) is 1, the key is currently pressed down.
            bool f8IsPressed = (GetAsyncKeyState(VK_F8) & 0x8000) != 0;
            bool f9IsPressed = (GetAsyncKeyState(VK_F9) & 0x8000) != 0;
            bool addIsPressed = (GetAsyncKeyState(VK_ADD) & 0x8000) != 0;
            bool subtractIsPressed = (GetAsyncKeyState(VK_SUBTRACT) & 0x8000) != 0;
            bool numpad1Pressed = (GetAsyncKeyState(VK_NUMPAD1) & 0x8000) != 0;
            bool numpad2Pressed = (GetAsyncKeyState(VK_NUMPAD2) & 0x8000) != 0;
            bool numpad3Pressed = (GetAsyncKeyState(VK_NUMPAD3) & 0x8000) != 0;
            bool numpad4Pressed = (GetAsyncKeyState(VK_NUMPAD4) & 0x8000) != 0;

            // Hardware Stat Numpad checks
            bool numpad5Pressed = (GetAsyncKeyState(VK_NUMPAD5) & 0x8000) != 0;
            bool numpad6Pressed = (GetAsyncKeyState(VK_NUMPAD6) & 0x8000) != 0;
            bool numpad7Pressed = (GetAsyncKeyState(VK_NUMPAD7) & 0x8000) != 0;
            bool numpad8Pressed = (GetAsyncKeyState(VK_NUMPAD8) & 0x8000) != 0;
            bool numpad9Pressed = (GetAsyncKeyState(VK_NUMPAD9) & 0x8000) != 0;

            //Crosshair and Panic Mode and Clock
            bool f12IsPressed = (GetAsyncKeyState(VK_F12) & 0x8000) != 0;
            bool numpad0Pressed = (GetAsyncKeyState(VK_NUMPAD0) & 0x8000) != 0;

            //Clock and clear note logic
            bool ctrlIsPressed = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            bool f11IsPressed = (GetAsyncKeyState(VK_F11) & 0x8000) != 0;

            // Wipe Notes (Ctrl + F11)
            if (ctrlIsPressed && f11IsPressed && !_ctrlF11WasPressed)
            {
                _noteText = ".";
            }
            _ctrlF11WasPressed = ctrlIsPressed && f11IsPressed;

            // System Clock Toggle (Only F11, NO Ctrl)
            if (!ctrlIsPressed && f11IsPressed && !_f11WasPressed)
            {
                // Your existing clock toggle logic here, for example:
                _clockVisible = !_clockVisible;
            }
            _f11WasPressed = f11IsPressed && !ctrlIsPressed;



            // --- DEBOUNCING AND LOGIC EXECUTION ---
            // Only trigger if pressed NOW, but was NOT pressed in the previous frame.

            // --- FLOATING NOTES LOGIC ---
            bool pgUpIsPressed = (GetAsyncKeyState(VK_PRIOR) & 0x8000) != 0;
            bool pgDnIsPressed = (GetAsyncKeyState(VK_NEXT) & 0x8000) != 0;

            // Toggle Visibility (PgDn)
            if (pgDnIsPressed && !_pgDnWasPressed) { _notesVisible = !_notesVisible; }
            _pgDnWasPressed = pgDnIsPressed;

            // Toggle Writing/Move Mode (PgUp)
            if (pgUpIsPressed && !_pgUpWasPressed) { _writingMode = !_writingMode; }
            _pgUpWasPressed = pgUpIsPressed;

            // If Writing Mode is active, hijack the keys for typing and moving
            if (_writingMode)
            {
                // 1. Scalable Smooth Movement (Scale speed based on screen resolution)
                float moveSpeed = _screenHeight * 0.005f;
                if ((GetAsyncKeyState(VK_LEFT) & 0x8000) != 0) _noteX -= moveSpeed;
                if ((GetAsyncKeyState(VK_RIGHT) & 0x8000) != 0) _noteX += moveSpeed;
                if ((GetAsyncKeyState(VK_UP) & 0x8000) != 0) _noteY -= moveSpeed;
                if ((GetAsyncKeyState(VK_DOWN) & 0x8000) != 0) _noteY += moveSpeed;

                // 2. Typing A-Z (Hex 0x41 to 0x5A)
                for (int i = 0x41; i <= 0x5A; i++)
                {
                    bool isKeyPressed = (GetAsyncKeyState(i) & 0x8000) != 0;
                    if (isKeyPressed && !_keyWasPressed[i])
                    {
                        if (_noteText == "Type here...") _noteText = "";
                        _noteText += (char)i;
                    }
                    _keyWasPressed[i] = isKeyPressed;
                }
                
                // 3. Spacebar
                bool spaceIsPressed = (GetAsyncKeyState(VK_SPACE) & 0x8000) != 0;
                if (spaceIsPressed && !_spaceWasPressed) { _noteText += " "; }
                _spaceWasPressed = spaceIsPressed;


                // 4. Backspace
                bool backIsPressed = (GetAsyncKeyState(VK_BACK) & 0x8000) != 0;
                if (backIsPressed && !_backWasPressed)
                {
                    if (_noteText.Length > 0)
                    {
                        _noteText = _noteText.Substring(0, _noteText.Length - 1);
                    }
                }
                _backWasPressed = backIsPressed;

                // 5. Enter (New Line)
                bool enterIsPressed = (GetAsyncKeyState(VK_RETURN) & 0x8000) != 0;
                if (enterIsPressed && !_enterWasPressed)
                {
                    if (_noteText == "Type here...") _noteText = "";
                    _noteText += "\n";
                }
                _enterWasPressed = enterIsPressed;
            }

            // Toggle Panic Mode
            if (f12IsPressed && !_f12WasPressed) { _panicMode = !_panicMode; }
            _f12WasPressed = f12IsPressed;

            // Toggle Crosshair
            if (numpad0Pressed && !_numpad0WasPressed) { _crosshairVisible = !_crosshairVisible; }
            _numpad0WasPressed = numpad0Pressed;

            // Toggle System Clock
            if (f11IsPressed && !_f11WasPressed) { _clockVisible = !_clockVisible; }
            _f11WasPressed = f11IsPressed;

            // Toggle Kill Counter Visibility
            if (f8IsPressed && !_f8WasPressed) { _isVisible = !_isVisible; }
            _f8WasPressed = f8IsPressed;

            // Reset Kill Counter
            if (f9IsPressed && !_f9WasPressed) { _counter = 0; }
            _f9WasPressed = f9IsPressed;

            // Increment Kill Counter
            if (addIsPressed && !_addWasPressed) { _counter++; }
            _addWasPressed = addIsPressed;

            // Decrement Kill Counter
            if (subtractIsPressed && !_subtractWasPressed) { _counter--; }
            _subtractWasPressed = subtractIsPressed;

            // Stopwatch: Play/Pause
            if (numpad1Pressed && !_numpad1WasPressed)
            {
                if (_stopwatch.IsRunning) _stopwatch.Stop();
                else _stopwatch.Start();
            }
            _numpad1WasPressed = numpad1Pressed;

            // Stopwatch: Print Lap to Console AND Save for Overlay (NEW)
            if (numpad2Pressed && !_numpad2WasPressed)
            {
                _lastLapTime = _stopwatch.Elapsed;
                _hasLapTime = true;
                Console.WriteLine($"[LAP] {_lastLapTime:hh\\:mm\\:ss\\.fff}");
            }
            _numpad2WasPressed = numpad2Pressed;

            // Stopwatch: Reset (NEW: Also resets lap time)
            if (numpad3Pressed && !_numpad3WasPressed)
            {
                _stopwatch.Reset();
                _hasLapTime = false;
            }
            _numpad3WasPressed = numpad3Pressed;

            // Stopwatch: Toggle Visibility
            if (numpad4Pressed && !_numpad4WasPressed) { _stopwatchVisible = !_stopwatchVisible; }
            _numpad4WasPressed = numpad4Pressed;

            // Hardware Stat Toggles
            if (numpad5Pressed && !_numpad5WasPressed) { _cpuTempVisible = !_cpuTempVisible; }
            _numpad5WasPressed = numpad5Pressed;

            if (numpad6Pressed && !_numpad6WasPressed) { _gpuTempVisible = !_gpuTempVisible; }
            _numpad6WasPressed = numpad6Pressed;

            if (numpad7Pressed && !_numpad7WasPressed) { _gpuLoadVisible = !_gpuLoadVisible; }
            _numpad7WasPressed = numpad7Pressed;

            if (numpad8Pressed && !_numpad8WasPressed) { _fpsVisible = !_fpsVisible; }
            _numpad8WasPressed = numpad8Pressed;

            // Toggle CPU Load
            if (numpad9Pressed && !_numpad9WasPressed) { _cpuLoadVisible = !_cpuLoadVisible; }
            _numpad9WasPressed = numpad9Pressed;

            // --- RENDERING ---

            // Erase the previous frame. Critical, otherwise text will stack into a solid blur.
            gfx.ClearScene();

            // Calculate dynamic spacing
            float margin = _screenHeight * 0.005f;
            float shadowOffset = Math.Max(2, _screenHeight * 0.002f);

            // --- PANIC MODE ---
            // If panic mode is active, fill the entire screen and skip drawing everything else
            if (_panicMode)
            {
                gfx.FillRectangle(_blackBrush, 0, 0, _screenWidth, _screenHeight);
                return;
            }

            // --- CROSSHAIR ---
            if (_crosshairVisible)
            {
                float centerX = _screenWidth / 2.0f;
                float centerY = _screenHeight / 2.0f;
                float dotRadius = 3.0f; // Change this number to make the dot bigger or smaller

                gfx.FillCircle(_redBrush, centerX, centerY, dotRadius);
            }

            // Draw Kill Counter (Top Left)
            if (_isVisible)
            {
                string textToDraw = $"{_counterText}: {_counter}";
                // Draw shadow first (offset slightly down and right)
                gfx.DrawText(_font, _blackBrush, margin + shadowOffset, margin + shadowOffset, textToDraw);
                // Draw main text over it
                gfx.DrawText(_font, _greenBrush, margin, margin, textToDraw);
            }

            // --- FLOATING NOTES RENDERING ---
            if (_notesVisible)
            {
                if (_writingMode)
                {
                    string helperText = "[EDIT MODE ACTIVE - PRESS PGUP TO SAVE]";

                    var noteSize = gfx.MeasureString(_smallfont, _noteText + "_");
                    var helperSize = gfx.MeasureString(_smallfont, helperText);

                    // Make padding scalable based on screen height
                    float padding = _screenHeight * 0.0005f;

                    // Draw a dark semi-transparent background so you know it's selected
                    gfx.FillRectangle(gfx.CreateSolidBrush(0, 0, 0, 150), _noteX - padding, _noteY - padding, _noteX + noteSize.X + padding, _noteY + noteSize.Y + padding);

                    // Draw text with an underscore to look like a text editor
                    gfx.DrawText(_smallfont, _greenBrush, _noteX, _noteY, _noteText + "_");

                    // Calculate Y position for helper text so it sits perfectly above the note box
                    float helperY = _noteY - helperSize.Y - padding - margin;
                    gfx.DrawText(_smallfont, _blueBrush, _noteX, helperY, helperText);
                }
                else
                {
                    if (!string.IsNullOrEmpty(_noteText))
                    {
                        // Not editing, just draw the normal text with a shadow
                        gfx.DrawText(_smallfont, _blackBrush, _noteX + shadowOffset, _noteY + shadowOffset, _noteText);
                        gfx.DrawText(_smallfont, _greenBrush, _noteX, _noteY, _noteText);
                    }
                }
            }

            // Draw Stopwatch and Lap Time (Top Right)
            if (_stopwatchVisible)
            {
                string stopwatchText = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss\.fff");
                var textSize = gfx.MeasureString(_stopwatchFont, stopwatchText);

                // Calculate X position so it right-aligns perfectly
                float stopwatchX = _screenWidth - textSize.X - margin;
                float stopwatchY = margin;

                // Draw main stopwatch
                gfx.DrawText(_stopwatchFont, _blackBrush, stopwatchX + shadowOffset, stopwatchY + shadowOffset, stopwatchText);
                gfx.DrawText(_stopwatchFont, _greenBrush, stopwatchX, stopwatchY, stopwatchText);

                // Draw Lap Time underneath if it exists (NEW)
                if (_hasLapTime)
                {
                    string lapText = $"Last Lap: {_lastLapTime.ToString(@"hh\:mm\:ss\.fff")}";
                    var lapTextSize = gfx.MeasureString(_smallfont, lapText);

                    float lapX = _screenWidth - lapTextSize.X - margin;
                    float lapY = stopwatchY + textSize.Y; // Place directly below the main stopwatch text

                    gfx.DrawText(_smallfont, _blackBrush, lapX + shadowOffset, lapY + shadowOffset, lapText);
                    gfx.DrawText(_smallfont, _greenBrush, lapX, lapY, lapText);
                }
            }

            // --- DRAW REAL HWINFO SENSORS (Mid-Left) ---
            if (_hwinfoReady)
            {
                // Dynamically build the string based on what the user has toggled ON
                string hwinfoText = "";

                // Append strings with newline characters if their flag is true
                if (_cpuTempVisible) hwinfoText += $"CPU: {_cpuTemp:0}°C\n";
                if (_cpuLoadVisible) hwinfoText += $"CPU Load: {_cpuUsage:0}%\n";
                if (_gpuTempVisible) hwinfoText += $"GPU: {_gpuTemp:0}°C\n";
                if (_gpuLoadVisible) hwinfoText += $"GPU Load: {_gpuUsage:0}%\n";
                if (_fpsVisible && _fps > 0) hwinfoText += $"FPS: {_fps:0}\n";

                // Trim the final invisible "newline" gap from the bottom to keep alignment clean
                hwinfoText = hwinfoText.TrimEnd('\n');

                if (!string.IsNullOrEmpty(hwinfoText))
                {
                    float x = margin;
                    float y = _screenHeight * 0.10f; // Start drawing 10% down the screen

                    // Draw shadow then text
                    gfx.DrawText(_smallfont, _blackBrush, x + shadowOffset, y + shadowOffset, hwinfoText);
                    gfx.DrawText(_smallfont, _greenBrush, x, y, hwinfoText);
                }
            }

            // --- SYSTEM CLOCK (Bottom Right) ---
            if (_clockVisible)
            {
                // Gets the current local time in a clean 12-hour format with AM/PM
                string timeText = DateTime.Now.ToString("h:mm tt");
                var timeSize = gfx.MeasureString(_smallfont, timeText);

                // Calculate X and Y to push it perfectly into the bottom right corner
                float clockX = _screenWidth - timeSize.X - margin;
                float clockY = _screenHeight - timeSize.Y - margin;

                // Draw shadow then text
                gfx.DrawText(_smallfont, _blackBrush, clockX + shadowOffset, clockY + shadowOffset, timeText);
                gfx.DrawText(_smallfont, _greenBrush, clockX, clockY, timeText);
            }
        }

        // Checks if HWiNFO is running. If not, attempts to start it from default install paths.
        public static void EnsureHwinfoRunning()
        {
            var running = Process.GetProcessesByName("HWiNFO64").Length > 0 ||
                          Process.GetProcessesByName("HWiNFO32").Length > 0;

            if (running)
            {
                Console.WriteLine("HWiNFO already running.");
                return;
            }

            string[] paths =
            {
                @"C:\Program Files\HWiNFO64\HWiNFO64.exe",
                @"C:\Program Files (x86)\HWiNFO64\HWiNFO64.exe"
            };

            foreach (var p in paths)
            {
                if (File.Exists(p))
                {
                    Console.WriteLine("Starting HWiNFO...");
                    Process.Start(p);
                    return;
                }
            }

            Console.WriteLine("HWiNFO not found. Please start it manually.");
        }

        // Creates a background thread that constantly polls HWiNFO shared memory every second.
        private static void StartSensorThread()
        {
            _sensorThread = new Thread(() =>
            {
                bool firstRunDebug = true;

                // Infinite loop running on a background thread
                while (true)
                {
                    try
                    {
                        MemoryMappedFile mmf = null;

                        // Step 1: Open the Shared Memory file created by HWiNFO
                        try
                        {
                            // Try user-space memory map first
                            mmf = MemoryMappedFile.OpenExisting("HWiNFO_SENS_SM2", MemoryMappedFileRights.Read);
                        }
                        catch
                        {
                            // Fallback to global-space (requires HWiNFO to be run as admin usually)
                            mmf = MemoryMappedFile.OpenExisting(@"Global\HWiNFO_SENS_SM2", MemoryMappedFileRights.Read);
                        }

                        // Use 'using' blocks to guarantee memory handles are released, preventing memory leaks
                        using (mmf)
                        using (var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
                        {
                            var handle = accessor.SafeMemoryMappedViewHandle;
                            IntPtr pointer = handle.DangerousGetHandle(); // Get raw memory pointer

                            // Step 2: Read the Master Header
                            var header = Marshal.PtrToStructure<HWiNFO_SENSORS_SHARED_MEM2>(pointer);

                            // Validate the signature to ensure we aren't reading garbage memory
                            if (header.dwSignature == 0x53695748)
                            {
                                // Flags to track if we found what we need during this pass
                                bool foundCpuTemp = false, foundCpuLoad = false, foundGpuTemp = false, foundGpuLoad = false, foundFps = false;

                                // Step 3: Iterate through every sensor reading available
                                for (uint i = 0; i < header.dwNumReadingElements; i++)
                                {
                                    // Calculate the exact memory address for sensor 'i'
                                    long offset = header.dwOffsetOfReadingSection + (i * header.dwSizeOfReadingElement);
                                    IntPtr elementPtr = IntPtr.Add(pointer, (int)offset);

                                    // Marshal the raw bytes back into our C# struct
                                    var element = Marshal.PtrToStructure<HWiNFO_ELEMENT>(elementPtr);

                                    // Clean up strings (remove null terminators \0 and extra spaces)
                                    string labelOrig = element.szLabelOrig?.Trim('\0', ' ') ?? "";
                                    string labelUser = element.szLabelUser?.Trim('\0', ' ') ?? "";
                                    string unit = element.szUnit?.Trim('\0', ' ') ?? "";

                                    // Prefer user-defined label if it exists, otherwise original
                                    string label = string.IsNullOrWhiteSpace(labelUser) ? labelOrig : labelUser;

                                    // Check sensor type (Temp vs Load/Usage)
                                    bool isTemp = (element.tReading == 1);
                                    bool isUsage = (element.tReading == 7);

                                    // --- PATTERN MATCHING TO FIND DESIRED SENSORS ---
                                    // Because different hardware (AMD vs Intel, NVIDIA vs AMD) names sensors differently,
                                    // we use label.Contains() with common keywords.

                                    // Match CPU Temp
                                    if (!foundCpuTemp && isTemp && (label.Contains("CPU Package") || label.Contains("CPU (Tctl/Tdie)") || label.Contains("Core Temperatures")))
                                    {
                                        _cpuTemp = (float)element.value;
                                        foundCpuTemp = true;
                                    }
                                    // Match CPU Load
                                    else if (!foundCpuLoad && isUsage && label.Contains("Total CPU Usage"))
                                    {
                                        _cpuUsage = (float)element.value;
                                        foundCpuLoad = true;
                                    }
                                    // Match GPU Temp
                                    else if (!foundGpuTemp && isTemp && (label.Contains("GPU Temperature") || label.Contains("GPU Core")))
                                    {
                                        _gpuTemp = (float)element.value;
                                        foundGpuTemp = true;
                                    }
                                    // Match GPU Load
                                    else if (!foundGpuLoad && isUsage && (label.Contains("GPU Core Load") || label.Contains("GPU Utilization") || label.Contains("GPU D3D Usage")))
                                    {
                                        _gpuUsage = (float)element.value;
                                        foundGpuLoad = true;
                                    }
                                    // Match RTSS FPS (Often provided by RivaTuner passing data to HWiNFO)
                                    else if (!foundFps && (label.Contains("Framerate") || unit.Contains("FPS")))
                                    {
                                        _fps = (float)element.value;
                                        foundFps = true;
                                    }

                                    // Optimization: Break out of the loop early if we found everything we need.
                                    // (Unless it's the first run, in which case we might want to let it loop for debugging purposes).
                                    if (foundCpuTemp && foundCpuLoad && foundGpuTemp && foundGpuLoad && foundFps && !firstRunDebug)
                                        break;
                                }

                                if (firstRunDebug) firstRunDebug = false;

                                // If we found at least one valid reading, tell the UI it's safe to draw hardware stats
                                _hwinfoReady = foundCpuTemp || foundCpuLoad || foundGpuTemp || foundGpuLoad;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // If HWiNFO is closed, crashes, or memory map goes away, safely hide the UI element 
                        // and silently wait for it to come back online.
                        _hwinfoReady = false;
                    }

                    // Poll every 1000 milliseconds (1 second) to minimize CPU impact
                    Thread.Sleep(1000);
                }
            });

            _sensorThread.IsBackground = true; // Ensures this thread dies when the main application closes
            _sensorThread.Start();
        }

        // Triggered when the window closes. Essential for unmanaged resource cleanup to prevent memory leaks.
        private static void Window_DestroyGraphics(object sender, DestroyGraphicsEventArgs e)
        {
            _font?.Dispose();
            _smallfont?.Dispose();
            _stopwatchFont?.Dispose();
            _greenBrush?.Dispose();
            _blackBrush?.Dispose();
            _redBrush?.Dispose();
            _blueBrush?.Dispose();

        }
    }
}