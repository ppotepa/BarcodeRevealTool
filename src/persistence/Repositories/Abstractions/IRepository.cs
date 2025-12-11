namespace BarcodeRevealTool.Persistence.Repositories.Abstractions
{
    /// <summary>
    /// Generic repository interface for CRUD operations.
    /// All entity repositories should implement this for consistent data access patterns.
    /// </summary>
    public interface IRepository<T> where T : class
    {
        /// <summary>Add a new entity to the database.</summary>
        Task<long> AddAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>Add multiple entities to the database.</summary>
        Task<int> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>Get an entity by ID.</summary>
        Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Get all entities matching a predicate.</summary>
        Task<IReadOnlyList<T>> GetAllAsync(Func<T, bool>? predicate = null, CancellationToken cancellationToken = default);

        /// <summary>Update an existing entity.</summary>
        Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default);

        /// <summary>Update multiple entities.</summary>
        Task<int> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

        /// <summary>Delete an entity by ID.</summary>
        Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Delete multiple entities.</summary>
        Task<int> DeleteRangeAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default);

        /// <summary>Check if an entity exists by ID.</summary>
        Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default);

        /// <summary>Count all entities matching a predicate.</summary>
        Task<int> CountAsync(Func<T, bool>? predicate = null, CancellationToken cancellationToken = default);
    }
}
