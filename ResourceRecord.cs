using System;

namespace MistLegacyBot
{
    internal enum ResourceGroupMode
    {
        None = 0,
        Skill = 1,
        Region = 2,
        Rarity = 3,
        Tier = 4
    }

    internal sealed class ResourceRecord
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Rarity { get; set; }
        public int Level { get; set; }
        public string Region { get; set; }
        public string Skill { get; set; }
        public string SkillDifficulty { get; set; }
        public int? Quantity { get; set; }
        public string Cooldown { get; set; }
        public bool Visible { get; set; }
        public bool OnMap { get; set; }
        public bool CanGather { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public double? Distance { get; set; }

        public string TierLabel
        {
            get { return Level.ToString(); }
        }

        public string SkillLabel
        {
            get
            {
                if (string.IsNullOrEmpty(SkillDifficulty))
                    return Skill;
                return Skill + " " + SkillDifficulty;
            }
        }

        public string VisibleLabel
        {
            get
            {
                if (Quantity == null)
                    return "нет в live";
                return Visible && OnMap ? "вид" : "скрыт";
            }
        }

        public string GroupKey(ResourceGroupMode mode)
        {
            switch (mode)
            {
                case ResourceGroupMode.Skill:
                    return string.IsNullOrEmpty(Skill) ? "-" : Skill;
                case ResourceGroupMode.Region:
                    return string.IsNullOrEmpty(Region) ? "?" : Region;
                case ResourceGroupMode.Rarity:
                    return string.IsNullOrEmpty(Rarity) ? "?" : Rarity;
                case ResourceGroupMode.Tier:
                    return Level.ToString();
                default:
                    return string.Empty;
            }
        }
    }
}
