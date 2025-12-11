namespace BarcodeRevealTool.Persistence.Repositories.Entities
{
    /// <summary>
    /// Base class for all entities with common properties.
    /// </summary>
    public abstract class BaseEntity
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
