using Serilog;
using SqlKata;
using SqlKata.Compilers;
using System.Data.SQLite;
using BarcodeRevealTool.Persistence.Repositories.Abstractions;
using BarcodeRevealTool.Persistence.Repositories.Entities;

namespace BarcodeRevealTool.Persistence.Repositories
{
    /// <summary>
    /// Generic SQLite repository for all entities.
    /// Provides centralized CRUD operations using SqlKata.
    /// </summary>
    public class Repository<T> : IRepository<T> where T : BaseEntity, new()
    {
        private readonly string _connectionString;
        private readonly SqliteCompiler _compiler;
        private readonly ILogger _logger;
        private readonly string _tableName;

        public Repository(string connectionString, SqliteCompiler? compiler = null)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _compiler = compiler ?? new SqliteCompiler();
            _tableName = GetTableName();
            _logger = Log.ForContext<Repository<T>>();
        }

        /// <summary>Infer table name from entity type.</summary>
        private static string GetTableName()
        {
            var name = typeof(T).Name;
            // DebugSessionEntity -> DebugSession, LobbyFileEntity -> LobbyFile, etc
            if (name.EndsWith("Entity"))
                return name[..^6]; // Remove "Entity" suffix
            return name;
        }

        private SQLiteConnection CreateConnection()
        {
            var connection = new SQLiteConnection(_connectionString)
            {
                DefaultTimeout = 5
            };
            connection.Open();
            return connection;
        }

        public async Task<long> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            return await Task.Run(() =>
            {
                try
                {
                    using var connection = CreateConnection();

                    var dict = EntityToDictionary(entity);
                    dict.Remove("Id"); // Don't include Id for insert (auto-increment)

                    var query = new Query(_tableName).AsInsert(dict);
                    var compiled = _compiler.Compile(query);

                    using var command = connection.CreateCommand();
                    command.CommandText = compiled.Sql;
                    foreach (var binding in compiled.Bindings)
                    {
                        command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                    }
                    command.ExecuteNonQuery();

                    using var lastIdCommand = connection.CreateCommand();
                    lastIdCommand.CommandText = "SELECT last_insert_rowid()";
                    var newId = lastIdCommand.ExecuteScalar();

                    _logger.Debug("Added {EntityType} with ID {Id}", typeof(T).Name, newId);
                    return Convert.ToInt64(newId ?? 0);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to add {EntityType}", typeof(T).Name);
                    throw;
                }
            }, cancellationToken);
        }

        public async Task<int> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            var entityList = entities.ToList();
            if (!entityList.Any())
                return 0;

            int count = 0;
            foreach (var entity in entityList)
            {
                await AddAsync(entity, cancellationToken);
                count++;
            }
            return count;
        }

        public async Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = CreateConnection();

                    var query = new Query(_tableName).Where("Id", id);
                    var compiled = _compiler.Compile(query);

                    using var command = connection.CreateCommand();
                    command.CommandText = compiled.Sql;
                    foreach (var binding in compiled.Bindings)
                    {
                        command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                    }

                    using var reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        return DictionaryToEntity(reader);
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to get {EntityType} by ID {Id}", typeof(T).Name, id);
                    throw;
                }
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<T>> GetAllAsync(Func<T, bool>? predicate = null, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = CreateConnection();

                    var query = new Query(_tableName);
                    var compiled = _compiler.Compile(query);

                    var entities = new List<T>();
                    using var command = connection.CreateCommand();
                    command.CommandText = compiled.Sql;
                    foreach (var binding in compiled.Bindings)
                    {
                        command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                    }

                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var entity = DictionaryToEntity(reader);
                        if (predicate == null || predicate(entity))
                        {
                            entities.Add(entity);
                        }
                    }

                    _logger.Debug("Retrieved {Count} {EntityType} entities", entities.Count, typeof(T).Name);
                    return entities.AsReadOnly();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to get all {EntityType} entities", typeof(T).Name);
                    throw;
                }
            }, cancellationToken);
        }

        public async Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            entity.UpdatedAt = DateTime.UtcNow;

            return await Task.Run(() =>
            {
                try
                {
                    using var connection = CreateConnection();

                    var dict = EntityToDictionary(entity);
                    dict.Remove("Id");
                    dict.Remove("CreatedAt"); // Don't update created time

                    var query = new Query(_tableName).Where("Id", entity.Id).AsUpdate(dict);
                    var compiled = _compiler.Compile(query);

                    using var command = connection.CreateCommand();
                    command.CommandText = compiled.Sql;
                    foreach (var binding in compiled.Bindings)
                    {
                        command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                    }

                    int result = command.ExecuteNonQuery();
                    _logger.Debug("Updated {EntityType} with ID {Id}, affected rows: {Rows}", typeof(T).Name, entity.Id, result);
                    return result > 0;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to update {EntityType} with ID {Id}", typeof(T).Name, entity.Id);
                    throw;
                }
            }, cancellationToken);
        }

        public async Task<int> UpdateRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            int count = 0;
            foreach (var entity in entities)
            {
                if (await UpdateAsync(entity, cancellationToken))
                    count++;
            }
            return count;
        }

        public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = CreateConnection();

                    var query = new Query(_tableName).Where("Id", id).AsDelete();
                    var compiled = _compiler.Compile(query);

                    using var command = connection.CreateCommand();
                    command.CommandText = compiled.Sql;
                    foreach (var binding in compiled.Bindings)
                    {
                        command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                    }

                    int result = command.ExecuteNonQuery();
                    _logger.Debug("Deleted {EntityType} with ID {Id}, affected rows: {Rows}", typeof(T).Name, id, result);
                    return result > 0;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to delete {EntityType} with ID {Id}", typeof(T).Name, id);
                    throw;
                }
            }, cancellationToken);
        }

        public async Task<int> DeleteRangeAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default)
        {
            int count = 0;
            foreach (var id in ids)
            {
                if (await DeleteAsync(id, cancellationToken))
                    count++;
            }
            return count;
        }

        public async Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default)
        {
            var entity = await GetByIdAsync(id, cancellationToken);
            return entity != null;
        }

        public async Task<int> CountAsync(Func<T, bool>? predicate = null, CancellationToken cancellationToken = default)
        {
            var all = await GetAllAsync(predicate, cancellationToken);
            return all.Count;
        }

        /// <summary>Convert entity to dictionary for SqlKata queries.</summary>
        private static Dictionary<string, object?> EntityToDictionary(T entity)
        {
            var dict = new Dictionary<string, object?>();
            var properties = typeof(T).GetProperties();

            foreach (var prop in properties)
            {
                if (!prop.CanRead)
                    continue;

                var value = prop.GetValue(entity);

                // Handle datetime serialization
                if (value is DateTime dt)
                    value = dt.ToString("O");

                dict[prop.Name] = value ?? DBNull.Value;
            }

            return dict;
        }

        /// <summary>Convert database row to entity.</summary>
        private static T DictionaryToEntity(SQLiteDataReader reader)
        {
            var entity = new T();
            var properties = typeof(T).GetProperties();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var fieldName = reader.GetName(i);
                var prop = properties.FirstOrDefault(p => p.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

                if (prop == null || !prop.CanWrite)
                    continue;

                var value = reader.GetValue(i);
                if (value == DBNull.Value)
                {
                    prop.SetValue(entity, null);
                    continue;
                }

                // Handle datetime deserialization
                if (prop.PropertyType == typeof(DateTime))
                {
                    if (value is string str && DateTime.TryParse(str, out var dt))
                        prop.SetValue(entity, dt);
                }
                else if (prop.PropertyType == typeof(DateTime?))
                {
                    if (value is string str && DateTime.TryParse(str, out var dt))
                        prop.SetValue(entity, dt);
                    else
                        prop.SetValue(entity, null);
                }
                else if (prop.PropertyType == typeof(byte[]))
                {
                    prop.SetValue(entity, value);
                }
                else
                {
                    try
                    {
                        var convertedValue = Convert.ChangeType(value, prop.PropertyType);
                        prop.SetValue(entity, convertedValue);
                    }
                    catch
                    {
                        // If conversion fails, leave as is
                    }
                }
            }

            return entity;
        }
    }
}
