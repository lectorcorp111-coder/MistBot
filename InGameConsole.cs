using System.Collections.Generic;
using UnityEngine;

namespace MistLegacyBot
{
    internal sealed class InGameConsole
    {
        public const int VisibleLineCount = 10;
        private const int MaxStoredLines = 200;
        private const float LineHeight = 18f;
        private const float ViewportHeight = VisibleLineCount * LineHeight;

        /// <summary>Viewport + box padding (6+8) + box margin (4+6).</summary>
        public const float LayoutHeight = ViewportHeight + 14f + 10f;

        private readonly List<ConsoleLine> _lines = new List<ConsoleLine>();
        private Vector2 _scroll;
        private GUIStyle _boxStyle;
        private GUIStyle _lineStyle;
        private GUIStyle _warnLineStyle;
        private bool _scrollToBottom;

        public void Add(string message, bool warning)
        {
            if (string.IsNullOrEmpty(message))
                return;

            _lines.Add(new ConsoleLine(message, warning));
            while (_lines.Count > MaxStoredLines)
                _lines.RemoveAt(0);

            _scrollToBottom = true;
        }

        public void Draw()
        {
            EnsureStyles();

            GUILayout.BeginVertical(_boxStyle, GUILayout.ExpandWidth(true));
            _scroll = GUILayout.BeginScrollView(
                _scroll,
                false,
                true,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(ViewportHeight));

            foreach (var line in _lines)
            {
                var style = line.Warning ? _warnLineStyle : _lineStyle;
                GUILayout.Label(line.Text, style, GUILayout.MinHeight(LineHeight));
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            if (_scrollToBottom)
            {
                _scroll.y = float.MaxValue;
                _scrollToBottom = false;
            }
        }

        private void EnsureStyles()
        {
            if (_boxStyle != null)
                return;

            var black = MakeTexture(new Color(0.06f, 0.06f, 0.06f, 0.95f));

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(6, 6, 6, 8),
                margin = new RectOffset(0, 0, 4, 6),
            };
            _boxStyle.normal.background = black;

            _lineStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = false,
                clipping = TextClipping.Overflow,
                fontSize = 12,
                padding = new RectOffset(0, 4, 2, 2),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.75f, 0.9f, 0.75f) },
            };

            _warnLineStyle = new GUIStyle(_lineStyle)
            {
                normal = { textColor = new Color(1f, 0.82f, 0.45f) },
            };
        }

        private static Texture2D MakeTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private readonly struct ConsoleLine
        {
            public ConsoleLine(string text, bool warning)
            {
                Text = text;
                Warning = warning;
            }

            public string Text { get; }
            public bool Warning { get; }
        }
    }
}
