using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MistLegacyBot
{
    internal sealed class OverlayInput
    {
        private static readonly FieldInfo IgnoreMouseField = typeof(MouseButtonManager).GetField(
            "ignoreMouse",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private MouseButtonManager _mouseManager;
        private bool _setIgnoreMouse;
        private EventSystem _pausedEventSystem;

        public void Update(bool overOverlay)
        {
            SetIgnoreMouse(overOverlay);
            PauseEventSystem(overOverlay);
            if (overOverlay)
                Input.ResetInputAxes();
        }

        public void Release()
        {
            SetIgnoreMouse(false);
            PauseEventSystem(false);
        }

        public static bool ContainsGuiPoint(Rect window, Vector2 guiPoint)
        {
            return window.Contains(guiPoint);
        }

        public static Vector2 GuiMouse()
        {
            var mouse = Input.mousePosition;
            return new Vector2(mouse.x, Screen.height - mouse.y);
        }

        public static void ConsumeIfOver(bool overOverlay)
        {
            var ev = UnityEngine.Event.current;
            if (ev == null || !overOverlay)
                return;
            if (ev.type == EventType.Layout || ev.type == EventType.Repaint)
                return;
            if (ev.isMouse || ev.type == EventType.ScrollWheel)
                ev.Use();
        }

        private void SetIgnoreMouse(bool ignore)
        {
            if (IgnoreMouseField == null)
                return;

            if (_mouseManager == null)
                _mouseManager = Object.FindObjectOfType<MouseButtonManager>();

            if (_mouseManager == null)
                return;

            if (ignore)
            {
                IgnoreMouseField.SetValue(_mouseManager, true);
                _setIgnoreMouse = true;
            }
            else if (_setIgnoreMouse)
            {
                IgnoreMouseField.SetValue(_mouseManager, false);
                _setIgnoreMouse = false;
            }
        }

        private void PauseEventSystem(bool pause)
        {
            if (pause)
            {
                var es = EventSystem.current != null ? EventSystem.current : _pausedEventSystem;
                if (es == null)
                    es = Object.FindObjectOfType<EventSystem>();
                if (es == null)
                    return;

                _pausedEventSystem = es;
                if (es.enabled)
                    es.enabled = false;
            }
            else if (_pausedEventSystem != null)
            {
                _pausedEventSystem.enabled = true;
                _pausedEventSystem = null;
            }
        }
    }
}
