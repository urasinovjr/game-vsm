using UnityEngine;
using UnityEngine.InputSystem;

namespace GameVSM
{
    // One place that decides which controls hints describe. On-screen touch controls
    // exist only on mobile builds, so desktop always shows keys even with a touch screen;
    // on a phone a connected keyboard switches hints to keys until the screen is touched.
    public static class InputHints
    {
        public const string ActionButton = "Действие", MenuButton = "Задание";
        static int frame = -1;
        static bool touch, known;

        public static bool Touch
        {
            get
            {
                if (frame == Time.frameCount) return touch;
                frame = Time.frameCount;
                if (!known) { touch = Application.isMobilePlatform; known = true; }
                if (!Application.isMobilePlatform) return touch = false;
                if (Touchscreen.current?.primaryTouch.press.wasPressedThisFrame == true) touch = true;
                else if (Keyboard.current?.anyKey.wasPressedThisFrame == true) touch = false;
                return touch;
            }
        }

        public static string Action(string verb) => Touch ? $"«{ActionButton}» — {verb}" : $"F — {verb}";
        public static string Menu => Touch ? MenuButton : MenuButton + " / Esc";
        // Touch zones and buttons carry their own labels, so no separate legend is needed.
        public static string Controls => Touch ? "" : "WASD — идти    Мышь — осмотр    F — действие    Esc — задание";
    }
}
