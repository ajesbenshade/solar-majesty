// Harvest contract — pure C#, no MonoBehaviour.
namespace SolarMajesty
{
    public enum ResourceNodeType
    {
        Regolith = 0,
        Metals = 1,
        Ice = 2,
        Fissile = 3
    }

    /// <summary>
    /// A depletable world deposit. Implemented by the runtime <c>ResourceNode</c> behaviour so
    /// the economy can grant extract yield without referencing the scene layer.
    /// </summary>
    public interface IHarvestable
    {
        ResourceNodeType NodeType { get; }
        bool IsDepleted { get; }

        /// <summary>Consume up to amount; returns what was actually taken.</summary>
        int Harvest(int amount);
    }
}
