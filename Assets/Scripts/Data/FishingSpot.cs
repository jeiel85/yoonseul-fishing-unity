using System.Collections.Generic;

namespace YoonseulFishing.Data
{
    /// <summary>
    /// Fishing locations. Ported from the Kotlin <c>enum class FishingSpot(...)</c>
    /// — modelled as a class with static singleton instances (C# enums can't
    /// carry fields). <see cref="Id"/> is the stable string used for persistence
    /// and for the spot-bonus lookups in the species roll.
    /// </summary>
    public sealed class FishingSpot
    {
        public readonly string Id;
        public readonly string NameKo;
        public readonly string NameEn;
        public readonly string NameJa;
        public readonly string DescriptionKo;
        public readonly string DescriptionEn;
        public readonly string DescriptionJa;
        public readonly string Emoji;
        public readonly int MinLevel;

        private FishingSpot(string id, string nameKo, string nameEn, string nameJa,
            string descriptionKo, string descriptionEn, string descriptionJa, string emoji, int minLevel)
        {
            Id = id;
            NameKo = nameKo;
            NameEn = nameEn;
            NameJa = nameJa;
            DescriptionKo = descriptionKo;
            DescriptionEn = descriptionEn;
            DescriptionJa = descriptionJa;
            Emoji = emoji;
            MinLevel = minLevel;
        }

        public static readonly FishingSpot WindyValley = new FishingSpot(
            "windy_valley",
            "바람의 계곡", "Windy Valley", "風の谷",
            "맑고 포근한 바람이 부는 청정한 계곡가입니다. 평화로운 강바닥에서 자라난 다양한 민물고기를 낚을 수 있습니다.",
            "A clear, cozy valley stream with refreshing breezes. Ideal for catching a variety of peaceful freshwater fish.",
            "穏やかな風が吹く清らかな渓谷。平和な川底で育った様々な淡水魚が釣れます。",
            "🏞️", 1);

        public static readonly FishingSpot GalaxyLake = new FishingSpot(
            "galaxy_lake",
            "은하빛 호수", "Galaxy Lake", "銀河の湖",
            "호수가 온 우주의 은하수를 담은 듯 비현실적으로 반짝입니다. 신비하고 희귀한 어류들이 밤낮없이 노닙니다.",
            "The lake sparkles like the Milky Way galaxy. Mysterious and rare species hover in these deep, starry pools.",
            "全ての星空が溶け込んだような幻想的な湖。神秘的で珍しい魚たちが優雅に泳ぎ回ります。",
            "🌌", 2);

        public static readonly FishingSpot WaveBeach = new FishingSpot(
            "wave_beach",
            "파도소리 비치", "Wave Sound Beach", "波音の砂浜",
            "부드럽게 밀려오는 에메랄드빛 바다와 철썩이는 파도소리가 편안한 마음을 선사하는 넓은 해변입니다.",
            "An emerald sea where gentle, rolling waves and soft sounds bring immense peace and legendary marine life.",
            "エメラルドグリーンの砂浜に押し寄せる美しい波が、心安らぐ時間と大物の出会いをもたらします。",
            "🏖️", 4);

        public static readonly IReadOnlyList<FishingSpot> All = new List<FishingSpot> { WindyValley, GalaxyLake, WaveBeach };

        public static FishingSpot FromId(string id)
        {
            foreach (var s in All)
                if (s.Id == id) return s;
            return WindyValley;
        }
    }
}
