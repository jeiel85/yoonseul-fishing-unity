using System.Collections.Generic;
using UnityEngine;

namespace YoonseulFishing.Data
{
    /// <summary>A single reward line inside a celebration popup.</summary>
    public class CelebrationReward
    {
        public readonly string Type; // "coins", "bait_worm", "bait_shrimp", "bait_golden", "xp"
        public readonly int Count;
        public readonly string NameKo;
        public readonly string NameEn;

        public CelebrationReward(string type, int count, string nameKo, string nameEn)
        {
            Type = type;
            Count = count;
            NameKo = nameKo;
            NameEn = nameEn;
        }
    }

    /// <summary>Data for the celebration window shown on quest/achievement/collection claims.</summary>
    public class CelebrationData
    {
        public readonly string Id;
        public readonly string TitleKo;
        public readonly string TitleEn;
        public readonly string DescriptionKo;
        public readonly string DescriptionEn;
        public readonly string Type; // "DAILY_QUEST", "ACHIEVEMENT", "COLLECTION"
        public readonly IReadOnlyList<CelebrationReward> Rewards;
        public readonly string BadgeEmoji;

        public CelebrationData(string id, string titleKo, string titleEn, string descriptionKo,
            string descriptionEn, string type, IReadOnlyList<CelebrationReward> rewards, string badgeEmoji = "🏆")
        {
            Id = id;
            TitleKo = titleKo;
            TitleEn = titleEn;
            DescriptionKo = descriptionKo;
            DescriptionEn = descriptionEn;
            Type = type;
            Rewards = rewards;
            BadgeEmoji = badgeEmoji;
        }
    }

    /// <summary>Transient in-app toast shown when a fish is caught.</summary>
    public class NotificationAlert
    {
        public readonly string Title;
        public readonly string Message;
        public readonly string Emoji;
        public readonly Color Color;

        public NotificationAlert(string title, string message, string emoji, Color color)
        {
            Title = title;
            Message = message;
            Emoji = emoji;
            Color = color;
        }
    }
}
