using System;
using System.Runtime.InteropServices;
using GameOverlay.Drawing;
using GameOverlay.Windows;

namespace OverlayTestCounter
{
    class Program
    {
        // --- Windows API for Global Shortcuts ---
        // This function checks the hardware state of a keyboard key globally
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
        public static extern int GetSystemMetrics(int nIndex);

        // These are the magic numbers Windows uses to understand width and height
        const int SM_CXSCREEN = 0; // X (Width)
        const int SM_CYSCREEN = 1; // Y (Height)

        // Virtual Key Codes
        const int VK_F8 = 0x77; // Toggle Visibility
        const int VK_F9 = 0x78; // Reset Counter

        // Variables for our logic
        static int _counter = 0;
        static bool _isVisible = true;

        // "Debounce" variables so holding a key doesn't trigger 60 times a second
        static bool _f8WasPressed = false;
        static bool _f9WasPressed = false;

        // Graphics resources
        static Font _font;
        static SolidBrush _greenBrush;
        static SolidBrush _blackBrush;

        static void Main(string[] args)
        {
            // 1. Setup Graphics Configuration
            var gfx = new Graphics()
            {
                MeasureFPS = true,
                PerPrimitiveAntiAliasing = true,
                TextAntiAliasing = true
            };

            // --- NEW: Ask Windows for the screen resolution ---
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            Console.WriteLine($"Detected Resolution: {screenWidth}x{screenHeight}");

            // --- NEW: Plug the dynamic width and height into the window ---
            var window = new GraphicsWindow(0, 0, screenWidth, screenHeight, gfx)
            {
                FPS = 60,
                IsTopmost = true,
                IsVisible = true,
                Title = "My Gaming Overlay"
            };

            // 3. Subscribe to the GameOverlay events
            window.SetupGraphics += Window_SetupGraphics;
            window.DrawGraphics += Window_DrawGraphics;
            window.DestroyGraphics += Window_DestroyGraphics;

            // 4. Start a background timer to tick the counter every 1 second
            System.Timers.Timer timer = new System.Timers.Timer(1000);
            timer.Elapsed += (s, e) => { if (_isVisible) _counter++; };
            timer.Start();

            // 5. Start the overlay loop (This blocks the console from closing)
            Console.WriteLine("Overlay is running. Press F8 to hide/show, F9 to reset counter.");
            window.Create();
            window.Join();
        }

        private static void Window_SetupGraphics(object sender, SetupGraphicsEventArgs e)
        {
            var gfx = e.Graphics;

            // Initialize your fonts and colors here (do not do this inside DrawGraphics!)
            _font = gfx.CreateFont("Consolas", 48, bold: true);
            _greenBrush = gfx.CreateSolidBrush(50, 205, 50); // Lime Green
            _blackBrush = gfx.CreateSolidBrush(0, 0, 0);     // Black
        }

        private static void Window_DrawGraphics(object sender, DrawGraphicsEventArgs e)
        {
            var gfx = e.Graphics;

            // --- 1. GLOBAL SHORTCUT LOGIC ---
            // Bitwise & 0x8000 checks the highest bit, which tells us if the key is CURRENTLY pressed down
            bool f8IsPressed = (GetAsyncKeyState(VK_F8) & 0x8000) != 0;
            bool f9IsPressed = (GetAsyncKeyState(VK_F9) & 0x8000) != 0;

            // Toggle visibility on F8
            if (f8IsPressed && !_f8WasPressed)
            {
                _isVisible = !_isVisible;
                Console.WriteLine($"Overlay Visible: {_isVisible}");
            }
            _f8WasPressed = f8IsPressed; // Save state for next frame

            // Reset counter on F9
            if (f9IsPressed && !_f9WasPressed)
            {
                _counter = 0;
            }
            _f9WasPressed = f9IsPressed;

            // --- 2. DRAWING LOGIC ---
            // You MUST clear the scene every frame, otherwise text will overlap forever
            gfx.ClearScene();

            if (!_isVisible)
                return; // If hidden, we clear the scene and stop drawing

            // Draw a pseudo-drop shadow by drawing black text slightly offset
            gfx.DrawText(_font, _blackBrush, 52, 52, $"Counter: {_counter}");

            // Draw the actual green text over it
            gfx.DrawText(_font, _greenBrush, 50, 50, $"Counter: {_counter}");
        }

        private static void Window_DestroyGraphics(object sender, DestroyGraphicsEventArgs e)
        {
            // Clean up GPU resources when the app closes to prevent memory leaks
            _font?.Dispose();
            _greenBrush?.Dispose();
            _blackBrush?.Dispose();
        }
    }
}