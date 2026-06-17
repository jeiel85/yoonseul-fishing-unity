namespace YoonseulFishing.Data
{
    /// <summary>
    /// A single caught-fish record. Ported from the Room entity
    /// <c>CaughtFishEntity</c>. In the Unity port these are persisted as a JSON
    /// list (see SaveService) rather than a SQLite table, so <see cref="Id"/> is
    /// assigned by the save layer instead of an autoincrement column.
    /// <see cref="CaughtTime"/> is Unix epoch milliseconds.
    /// </summary>
    [System.Serializable]
    public class CaughtFish
    {
        public int Id;
        public string SpeciesId;
        public float Weight;
        public float Length;
        public long CaughtTime;
        public string TimeOfDay; // "day", "sunset", "night"

        public CaughtFish() { }

        public CaughtFish(string speciesId, float weight, float length, string timeOfDay, long caughtTime, int id = 0)
        {
            Id = id;
            SpeciesId = speciesId;
            Weight = weight;
            Length = length;
            TimeOfDay = timeOfDay;
            CaughtTime = caughtTime;
        }
    }
}
