namespace SolarMajesty
{
    public enum ShopItemKind
    {
        PermanentSuit = 0,
        ConsumableGene = 1
    }

    public enum ShopItemId
    {
        None = 0,
        SuitFieldShell = 1,
        SuitHardplate = 2,
        GeneCourage = 10,
        GeneWork = 11,
        GeneSwift = 12
    }

    /// <summary>Catalog entry for the waystation shop (credits, not stockpile).</summary>
    public sealed class ShopItemDef
    {
        public ShopItemId Id;
        public ShopItemKind Kind;
        public string DisplayName;
        public string Description;
        public int Cost;
        public float ArmorMitigation;
        public float SpeedBonus;
        public float WorkBonus;
        public float CourageBonus;
        public float DurationSeconds;
    }
}
