using System.Linq;
using Virtys.Common.Shared.Algorithms;
using Virtys.Nebula.Client.ViewModel.Domain;

namespace MistLegacyBot
{
    internal static class GatherService
    {
        public static bool TryGatherNearest(BotLogger log)
        {
            var player = PlayerVM.Current;
            if (player == null)
            {
                log.LogWarning("Gather: персонаж не в мире (PlayerVM.Current == null).");
                return false;
            }

            var group = player.Group;
            if (group == null)
            {
                log.LogWarning("Gather: нет отряда на карте.");
                return false;
            }

            if (group.Combat != null)
            {
                log.LogWarning("Gather: отряд в бою.");
                return false;
            }

            if (group.Gathering)
            {
                log.LogInfo("Gather: уже идёт сбор.");
                return false;
            }

            if (group.IsTraveling)
            {
                log.LogInfo("Gather: отряд в пути — дождись прибытия или нажми снова.");
                return false;
            }

            if (group.IsBusy)
            {
                log.LogInfo("Gather: отряд занят другим действием.");
                return false;
            }

            var resources = player.Resources;
            if (resources == null || resources.Count == 0)
            {
                log.LogWarning("Gather: список ресурсов пуст.");
                return false;
            }

            var nearest = resources
                .Where(r => r != null && r.Visible && r.OnMap && r.Quantity > 0)
                .OrderBy(r => Distance.Measure(r.X, r.Y, group.X, group.Y))
                .FirstOrDefault();

            if (nearest == null)
            {
                log.LogWarning("Gather: нет видимых ресурсов на карте.");
                return false;
            }

            var dist = Distance.Measure(nearest.X, nearest.Y, group.X, group.Y);
            var name = nearest.ItemBase != null ? nearest.ItemBase.Name : "?";
            log.LogInfo(
                $"Gather: ближайший «{name}» @ ({nearest.X},{nearest.Y}), dist={dist:F1}, qty={nearest.Quantity}.");

            if (!nearest.HasSkillToGather)
            {
                log.LogWarning("Gather: нет навыка / инструмента для этого ресурса.");
                if (!string.IsNullOrEmpty(nearest.ErrorMessage))
                    log.LogWarning($"  {nearest.ErrorMessage}");
                return false;
            }

            var container = group.ItemContainer;
            if (container == null)
            {
                log.LogWarning("Gather: нет инвентаря отряда.");
                return false;
            }

            var encumbrance = nearest.ItemBase != null ? nearest.ItemBase.Encumbrance : 0;
            if (container.FreeSpace < encumbrance)
            {
                log.LogWarning(
                    $"Gather: мало места в сумке (free={container.FreeSpace}, нужно ≥{encumbrance}).");
                return false;
            }

            if (!nearest.CanGather)
            {
                log.LogWarning("Gather: ресурс сейчас нельзя собрать.");
                if (!string.IsNullOrEmpty(nearest.ErrorMessage))
                    log.LogWarning($"  {nearest.ErrorMessage}");
                return false;
            }

            var started = nearest.Gather();
            if (started)
            {
                log.LogInfo("Gather: команда отправлена — идём / собираем.");
                return true;
            }

            log.LogWarning("Gather: Gather() вернул false.");
            if (!string.IsNullOrEmpty(nearest.ErrorMessage))
                log.LogWarning($"  {nearest.ErrorMessage}");
            return false;
        }
    }
}
