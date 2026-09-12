namespace SolarMajesty
{
    public enum ShopItemKind
    {
        PermanentSuit = 0,
        ConsumableGene = 1,
        ConsumablePotion = 2,
        Accessory = 3,
        Weapon = 4
    }

    public enum ShopVendor
    {
        Inn = 0,
        GuildHall = 1,
        Market = 2,
        Blacksmith = 3
    }

    public enum ShopItemId
    {
        None = 0,
        SuitFieldShell = 1,
        SuitHardplate = 2,
        GeneCourage = 10,
        GeneWork = 11,
        GeneSwift = 12,
        HorizonOptics = 20,
        AnvilRig = 21,
        AegisShell = 22,
        TriagePack = 23,
        HealthPotion = 30,
        MagicPotion = 31,
        RegenNecklace = 32,
        HorizonNeedle = 40,
        HorizonWeave = 41,
        AnvilSledge = 42,
        AnvilPlate = 43,
        AegisPike = 44,
        AegisMail = 45,
        TriageInjector = 46,
        TriageMail = 47
    }

    /// <summary>Catalog entry. Heroes spend personal credits; the colony spends Metals (CRED).</summary>
    public sealed class ShopItemDef
    {
        public ShopItemId Id;
        public ShopItemKind Kind;
        public ShopVendor Vendor;
        public SpecialistClass? ForClass;
        public string DisplayName;
        public string Description;
        public int Cost;
        public float ArmorMitigation;
        public float SpeedBonus;
        public float WorkBonus;
        public float CourageBonus;
        public float HealAmount;
        public float RegenPerSecond;
        public float DurationSeconds;
        public float DamageBonus;
    }
}
