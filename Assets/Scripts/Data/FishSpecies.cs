using System.Collections.Generic;
using UnityEngine;

namespace YoonseulFishing.Data
{
    /// <summary>
    /// Immutable metadata for a single fish species.
    /// Ported 1:1 from the Android source (data/FishSpecies.kt). The
    /// Jetpack Compose <c>Color(0xAARRGGBB)</c> literals are carried over as
    /// <see cref="UnityEngine.Color32"/> built from the RGB bytes (alpha forced
    /// to opaque, matching the original 0xFF alpha on every entry).
    /// </summary>
    [System.Serializable]
    public class FishSpecies
    {
        public string Id;
        public string Name;        // ko
        public string NameEn;
        public string ScientificName;
        public string Rarity;      // "일반", "희귀", "전설", "신화"
        public string RarityEn;    // "Common", "Rare", "Legendary", "Mythic"
        public string OptimalTime;     // "언제나", "낮", "노을", "밤"
        public string OptimalTimeEn;   // "Anytime", "Day", "Sunset", "Night"
        public float BaseLengthMin;
        public float BaseLengthMax;
        public float BaseWeightMin;
        public float BaseWeightMax;
        public string Description;
        public string DescriptionEn;
        public Color Color;

        public FishSpecies(
            string id, string name, string nameEn, string scientificName,
            string rarity, string rarityEn, string optimalTime, string optimalTimeEn,
            float baseLengthMin, float baseLengthMax, float baseWeightMin, float baseWeightMax,
            string description, string descriptionEn, Color color)
        {
            Id = id;
            Name = name;
            NameEn = nameEn;
            ScientificName = scientificName;
            Rarity = rarity;
            RarityEn = rarityEn;
            OptimalTime = optimalTime;
            OptimalTimeEn = optimalTimeEn;
            BaseLengthMin = baseLengthMin;
            BaseLengthMax = baseLengthMax;
            BaseWeightMin = baseWeightMin;
            BaseWeightMax = baseWeightMax;
            Description = description;
            DescriptionEn = descriptionEn;
            Color = color;
        }

        /// <summary>Builds an opaque colour from a 0xRRGGBB integer (Compose 0xFFRRGGBB minus alpha).</summary>
        private static Color Rgb(uint rgb)
        {
            byte r = (byte)((rgb >> 16) & 0xFF);
            byte g = (byte)((rgb >> 8) & 0xFF);
            byte b = (byte)(rgb & 0xFF);
            return new Color32(r, g, b, 0xFF);
        }

        public static readonly IReadOnlyList<FishSpecies> List = new List<FishSpecies>
        {
            new FishSpecies(
                "gold_crucian", "황금 붕어", "Golden Crucian Carp", "Carassius auratus",
                "일반", "Common", "언제나", "Anytime",
                15f, 25f, 0.2f, 0.6f,
                "가장 흔하면서도 친근한 민물고기. 따스한 황금빛 비늘이 특징이며, 평화로운 강가 어디서나 만날 수 있습니다. 물결을 따라 조용히 머무는 모습이 마음을 차분하게 만들어 줍니다.",
                "The most common and friendly freshwater fish. Characterized by warm golden scales, it is found everywhere in peaceful waters. Watching it float quietly along the waves brings calmness to your mind.",
                Rgb(0xFBE4A0)), // Pastel Gold
            new FishSpecies(
                "rainbow_trout", "무지개 송어", "Rainbow Trout", "Oncorhynchus mykiss",
                "희귀", "Rare", "낮", "Day",
                30f, 50f, 1.2f, 2.8f,
                "몸 옆면에 알록달록 고운 무지개 띠를 두른 물고기. 맑고 차가운 물살을 헤쳐 나가는 생명력이 돋보이며, 보는 이의 눈을 싱그럽게 만들어 줍니다.",
                "A fish adorned with colorful, beautiful rainbow bands along its body. Its vibrant life force swimming through clear, cool streams refreshes the observer's eyes.",
                Rgb(0xA5E6D8)), // Pastel Mint/Rainbow
            new FishSpecies(
                "sweetfish", "은어", "Sweetfish", "Plecoglossus altivelis",
                "일반", "Common", "낮", "Day",
                12f, 20f, 0.1f, 0.3f,
                "깨끗한 1급수 계곡에 고요히 서식하는 은빛 소형어류. 돌 표면의 이끼만 먹고 자라며, 몸에서 싱그러운 수박 향이 나는 자연의 정수를 품은 물고기입니다.",
                "A small silver fish residing quietly in pristine Grade-1 valley streams. Feeding only on rock moss, it carries a refreshing watermelon fragrance, embodying the essence of nature.",
                Rgb(0xE2EAF4)), // Pastel Silver-Blue
            new FishSpecies(
                "mandarin_fish", "쏘가리", "Mandarin Fish", "Siniperca scherzeri",
                "희귀", "Rare", "언제나", "Anytime",
                25f, 40f, 0.5f, 1.8f,
                "아름다운 호랑이 무늬 비늘을 가진 강강의 숨은 지배자. 어두운 바위 틈 사이에 몸을 숨기고 주변 물소리에 가만히 귀 기울이는 평화로운 습성을 가졌습니다.",
                "The hidden ruler of rivers with gorgeous tiger-like stripe patterns. It lies peacefully in dark rock crevices, quietly listening to the gentle flow of the streams.",
                Rgb(0xE7D1B9)), // Pastel Ochre
            new FishSpecies(
                "cherry_salmon", "산천어", "Cherry Salmon", "Oncorhynchus masou",
                "희귀", "Rare", "낮", "Day",
                20f, 35f, 0.3f, 0.9f,
                "울창한 숲속 맑고 차가운 물속에 살며 붉은 나뭇잎 무늬를 등 쪽에 새겨 둔 듯한 수려한 모습의 물고기. 존재를 마주하는 것만으로 맑은 바람이 느껴집니다.",
                "A gorgeous fish living in clear, cold streams of deep forests, decorated with crimson mountain-leaf patterns along its back. Its presence alone brings a cool, pleasant breeze to mind.",
                Rgb(0xF1C8DB)), // Pastel Rose
            new FishSpecies(
                "catfish", "메기", "Amur Catfish", "Silurus asotus",
                "일반", "Common", "밤", "Night",
                35f, 60f, 1.5f, 4.2f,
                "길고 동그란 수염을 느긋하게 흔들며 강바닥 모래 속에 살포시 기대어 지내는 어두운 밤을 사랑하는 묵묵한 강가의 오랜 친구입니다.",
                "A silent friend of the river who gently wiggles its long, round whiskers while resting comfortably deep in the riverbed sand, loving the tranquility of the starry night.",
                Rgb(0xC0CAD6)), // Pastel Slate Grey
            new FishSpecies(
                "shiri", "쉬리", "Splendid Dace", "Coreoleuciscus splendidus",
                "일반", "Common", "낮", "Day",
                10f, 14f, 0.05f, 0.15f,
                "한국의 물줄기를 반짝임으로 장식해 주는 청초한 물고기. 햇빛을 받을 때 지느러미 끝에 주황, 보라, 하늘 등 일곱 무지갯빛이 수줍게 부서집니다.",
                "A pure and clear fish decorating sparkling water streams. When exposed to bright sunlight, seven shades of rainbow timidly scatter from the tips of its translucent fins.",
                Rgb(0xCEE5F2)), // Clear Crystal Pastel
            new FishSpecies(
                "koi", "비단잉어", "Koi Carp", "Cyprinus rubrofuscus",
                "전설", "Legendary", "언제나", "Anytime",
                50f, 80f, 3.0f, 8.5f,
                "고혹적이고 기품 있는 맑은 흰 바탕에 진한 다홍 무늬를 자랑하는 물고기. 흐르는 우아한 춤사위는 낚시꾼의 우수에 찬 마음에 크나큰 평안을 실어다 줍니다.",
                "An elegant, highly valued fish boasting deep crimson patterns on a pure white background. Its flowing, graceful movements bring immense peace to any thoughtful observer.",
                Rgb(0xFFB2A0)), // Soft Coral Red
            new FishSpecies(
                "gobs", "가물치", "Northern Snakehead", "Channa argus",
                "희귀", "Rare", "언제나", "Anytime",
                45f, 75f, 2.0f, 5.5f,
                "강인한 생명력을 가졌으면서도 깊은 진흙 바닥 속에서 오래도록 침묵을 지키는 존재. 조용히 내면을 들여다보듯 가물치는 잔잔한 호숫가 아래를 조용히 묵수합니다.",
                "Possessing resilient life force yet keeping quiet deep within the muddy lakebeds. Like looking deep into one's inner self, it gazes silently beneath the calm waters.",
                Rgb(0xCFD5C4)), // Soft Sage Green
            new FishSpecies(
                "moonlight_catfish", "달빛 메기", "Moonlight Catfish", "Silurus lunaris",
                "전설", "Legendary", "밤", "Night",
                40f, 65f, 1.8f, 4.8f,
                "밤의 영혼처럼 은은하게 푸른 남빛으로 은화 모양 무늬를 가지는 신화적인 존재. 어둠 속에서 오직 밤하늘의 노란 달빛을 호흡하며 조용히 수면을 배회합니다.",
                "A mythical fish carrying glowing silver coin patterns on a soft lavender-blue body. In the deep dark, it breathes the yellow moonlight and quietly hovers near the surface.",
                Rgb(0xB5C3E8)), // Soft Lavender-Blue
            new FishSpecies(
                "sunset_butterfly", "노을 나비고기", "Sunset Butterflyfish", "Pterophyllum hesperis",
                "전설", "Legendary", "노을", "Sunset",
                18f, 25f, 0.4f, 1.0f,
                "해가 저무는 짧은 순간, 강가 표면이 붉은 융단을 이룰 때 수양버들 아래 사뿐히 떠올라 붉은 날갯짓을 하는 따뜻한 저녁의 등방어적 물고기입니다.",
                "At sunset when the water surface forms a glowing red carpet, it gently floats beneath weeping willows and flutters its warm orange fins resembling wings.",
                Rgb(0xFFCCAC)), // Pastel Orange-Warm Cream
            new FishSpecies(
                "star_whale", "별빛 고래", "Starlight Whale", "Astrodelphis stellula",
                "신화", "Mythic", "밤", "Night",
                120f, 200f, 25f, 60f,
                "온 우주의 성운과 빛나는 눈꽃 성좌를 등 비늘에 고이 수놓은 초자연적인 민물 고래. 한밤중 낚시꾼의 호숫가에서 단 한 번 공중으로 도약해 잊지 못할 은하수 분수를 발산합니다.",
                "A supernatural freshwater whale decorated with deep cosmic nebulae and sparkling star constellations on its back. It leaps once at midnight, splashing an unforgettable starry fountain.",
                Rgb(0xD4B2E8)), // Pastel Rich Violet
        };

        public static FishSpecies Find(string id) => ((List<FishSpecies>)List).Find(f => f.Id == id);
    }
}
