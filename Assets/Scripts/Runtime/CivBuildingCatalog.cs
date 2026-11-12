using System;
using System.Collections.Generic;

namespace SolarMajesty
{
    /// <summary>
    /// Where a civ's non-castle buildings load from, once the art exists. Castles stay in
    /// <see cref="CivCastleCatalog"/> (<c>Assets/Resources/Citadels</c>). These five drop in
    /// <c>Assets/Resources/CivBuildings</c> as one FBX per civ, named
    /// <c>SM_{architecture}_{building}</c>. The architecture token is the citadel file stem, so
    /// the set matches the castle the colony already draws:
    /// Ziggurat Keep → Ziggurat, Ring Citadel → Ring, Dome Bunker → Dome,
    /// Pathfinder Accord → Arcology (<c>SM_Citadel_Arcology</c>).
    ///
    /// Example: <c>Assets/Resources/CivBuildings/SM_Ziggurat_Inn.fbx</c>
    /// loads as <c>CivBuildings/SM_Ziggurat_Inn</c>. The same pattern covers
    /// <c>SM_Ziggurat_GuildHall</c>, <c>SM_Ziggurat_HAB</c>, <c>SM_Ziggurat_Power</c> and
    /// <c>SM_Ziggurat_Defense</c>, and the Ring, Dome and Arcology stems.
    ///
    /// Model on the catalog's 6 m plot (4×4 cells at 1.5 m), shorter than the Dome, scale 1,
    /// pivot at ground centre, front toward Blender -Y. The loader yaws 180° so the front faces
    /// -Z, the same turn the castles use. Material slots use the shared citadel trim sheet:
    /// Trim, Carbon, TeamColour, Accent, CoreEmission. LOD meshes, when a building has them,
    /// end in <c>_LOD0</c>, <c>_LOD1</c> and <c>_LOD2</c>.
    ///
    /// A missing file keeps the shared hero kit or the procedural kit the neutral Commons civ
    /// already uses. An empty prefab at one of these paths would count as the variant and hide
    /// that kit, so the drop is the FBX itself.
    /// </summary>
    public static class CivBuildingCatalog
    {
        /// <summary>Resources folder under Assets/Resources. Not Citadels, and not Buildings.</summary>
        public const string Root = "CivBuildings";

        /// <summary>Citadel resource path up to the architecture stem (<c>Citadels/SM_Citadel_Ziggurat</c>).</summary>
        public const string CitadelStemPrefix = "Citadels/SM_Citadel_";

        /// <summary>
        /// The buildings the architectural set covers first. Anything else keeps today's art
        /// even when a civ is bound. Habitat's file stem is HAB, Guild Hall's is GuildHall.
        /// </summary>
        private static readonly BuildingCategory[] BatchList =
        {
            BuildingCategory.Inn,
            BuildingCategory.GuildHall,
            BuildingCategory.Habitat,
            BuildingCategory.Power,
            BuildingCategory.Defense
        };

        public static IReadOnlyList<BuildingCategory> Batch => BatchList;

        /// <summary>FBX stem for <paramref name="category"/>, or null when this batch does not cover it.</summary>
        public static string Stem(BuildingCategory category)
        {
            switch (category)
            {
                case BuildingCategory.Inn: return "Inn";
                case BuildingCategory.GuildHall: return "GuildHall";
                case BuildingCategory.Habitat: return "HAB";
                case BuildingCategory.Power: return "Power";
                case BuildingCategory.Defense: return "Defense";
                default: return null;
            }
        }

        /// <summary>
        /// Architecture stem for the castle <paramref name="civ"/> actually draws (its
        /// <see cref="CivModifiers.CastleKey"/>), or null for the neutral civ and for a civ
        /// with no castle. Pathfinder Accord's stem is Arcology.
        /// </summary>
        public static string ArchitectureOf(CivModifiers civ)
        {
            if (civ == null || civ.Id == CivId.None) return null;
            var spec = CivCastleCatalog.ForKey(civ.CastleKey);
            if (spec == null || string.IsNullOrEmpty(spec.ResourcePath)) return null;
            if (!spec.ResourcePath.StartsWith(CitadelStemPrefix, StringComparison.Ordinal)) return null;
            string token = spec.ResourcePath.Substring(CitadelStemPrefix.Length);
            return token.Length == 0 ? null : token;
        }

        public static string ArchitectureOf(CivId civ) => ArchitectureOf(CivModifiers.For(civ));

        /// <summary>Resources path (no extension), or null when this civ has no variant of <paramref name="category"/>.</summary>
        public static string ResourcePath(CivModifiers civ, BuildingCategory category)
        {
            string architecture = ArchitectureOf(civ);
            string stem = Stem(category);
            if (string.IsNullOrEmpty(architecture) || string.IsNullOrEmpty(stem)) return null;
            return Root + "/SM_" + architecture + "_" + stem;
        }

        public static string ResourcePath(CivId civ, BuildingCategory category) =>
            ResourcePath(CivModifiers.For(civ), category);

        /// <summary>Project path of the FBX the artist drops, or null when there is no variant.</summary>
        public static string AssetPath(CivId civ, BuildingCategory category)
        {
            string resources = ResourcePath(civ, category);
            return resources == null ? null : "Assets/Resources/" + resources + ".fbx";
        }
    }
}
