using BepInEx.Logging;

namespace MistLegacyBot
{
    internal sealed class BotLogger
    {
        private readonly ManualLogSource _bepIn;
        private readonly InGameConsole _console;

        public BotLogger(ManualLogSource bepIn, InGameConsole console)
        {
            _bepIn = bepIn;
            _console = console;
        }

        public void LogInfo(string message)
        {
            _bepIn.LogInfo(message);
            _console.Add(message, warning: false);
        }

        public void LogWarning(string message)
        {
            _bepIn.LogWarning(message);
            _console.Add(message, warning: true);
        }
    }
}
