using BepInEx;
using UnityEngine;

namespace MistLegacyBot
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.mistlegacy.bot";
        public const string PluginName = "MistLegacyBot";
        public const string PluginVersion = "1.0.06";

        private BotPanel _panel;

        private void Awake()
        {
            _panel = new BotPanel(Logger);
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
                _panel.Toggle();
        }

        private void OnGUI()
        {
            _panel.Draw();
        }
    }
}
