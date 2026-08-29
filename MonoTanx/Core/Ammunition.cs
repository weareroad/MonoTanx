namespace MonoTanx.Core
{
    public sealed class Ammunition
    {
        public string Id { get; }
        public string Description { get; }
        public string SpriteId { get; }
        public string AudioEffectId { get; }
        public float ReloadTimeSeconds { get; }
        public float MaxFlightDurationSeconds { get; }
        public int Damage { get; }

        public Ammunition(string id, string description, string spriteId, string audioEffectId,
            float reloadTimeSeconds, float maxFlightDurationSeconds, int damage)
        {
            Id = id;
            Description = description;
            SpriteId = spriteId;
            AudioEffectId = audioEffectId;
            ReloadTimeSeconds = reloadTimeSeconds;
            MaxFlightDurationSeconds = maxFlightDurationSeconds;
            Damage = damage;
        }
    }
}
