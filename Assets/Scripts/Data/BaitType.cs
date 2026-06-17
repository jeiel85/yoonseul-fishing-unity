using System.Collections.Generic;

namespace YoonseulFishing.Data
{
    /// <summary>
    /// Bait definitions. Ported from the Kotlin <c>enum class BaitType(...)</c>;
    /// since C# enums can't carry fields, this is a class with static singleton
    /// instances. Reference equality works for the <c>== BaitType.Worm</c> style
    /// checks used throughout the game logic, and <see cref="Id"/> is the stable
    /// string used for persistence.
    /// </summary>
    public sealed class BaitType
    {
        public readonly string Id;
        public readonly string NameEn;
        public readonly string NameKo;
        public readonly string NameJa;
        public readonly int Price;
        public readonly string DescriptionEn;
        public readonly string DescriptionKo;
        public readonly string DescriptionJa;
        public readonly string Emoji;

        private BaitType(string id, string nameEn, string nameKo, string nameJa, int price,
            string descriptionEn, string descriptionKo, string descriptionJa, string emoji)
        {
            Id = id;
            NameEn = nameEn;
            NameKo = nameKo;
            NameJa = nameJa;
            Price = price;
            DescriptionEn = descriptionEn;
            DescriptionKo = descriptionKo;
            DescriptionJa = descriptionJa;
            Emoji = emoji;
        }

        public static readonly BaitType Basic = new BaitType(
            "basic", "Plain Paste", "기본 떡밥", "普通練り餌", 0,
            "Infinite plain bait.", "무제한으로 제공되는 기본 떡밥입니다.", "無限に使える基本的なエサです。", "🧆");

        public static readonly BaitType Worm = new BaitType(
            "worm", "Lugworm", "갯지렁이 미끼", "ゴカイの餌", 10,
            "Bite waiting time reduced by 30%.", "물고기가 찌를 건드리는 대기 시간이 30% 단축됩니다.", "一口が30%速くなります。", "🪱");

        public static readonly BaitType Shrimp = new BaitType(
            "shrimp", "Krill Shrimps", "크릴새우 미끼", "オキアミの餌", 30,
            "Rare or legendary rates increased.", "희귀 등급 이상을 낚을 확률이 눈에 띄게 올라갑니다.", "レア以上の出現率が大きくアップします。", "🦐");

        public static readonly BaitType Golden = new BaitType(
            "golden", "Golden Mash", "황금 고농축 떡밥", "黄金の練り餌", 85,
            "Significantly triples Mythical / Legendary encounter rates.", "전설 및 특히 신화 등급의 고대 생명체를 만날 확률이 극대화됩니다.", "伝説・神話クラスの出現率が劇的に増加します。", "🪙");

        public static readonly IReadOnlyList<BaitType> All = new List<BaitType> { Basic, Worm, Shrimp, Golden };

        public static BaitType FromId(string id)
        {
            foreach (var b in All)
                if (b.Id == id) return b;
            return Basic;
        }
    }
}
