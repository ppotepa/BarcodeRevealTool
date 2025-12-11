# Database Migrations

This folder contains all database schema migrations organized by version.

## Structure

```
Migrations/
├── V1_Initial/          # Initial schema setup
│   ├── 001_CreateRunInfo.sql
│   ├── 002_CreatePlayers.sql
│   ├── 003_CreateReplayFiles.sql
│   ├── 004_CreateDebugSession.sql
│   └── 005_CreateUserConfig.sql
├── V2_Future/           # Future changes
└── MigrationRunner.cs   # Migration execution engine
```

## How Migrations Work

1. **Versioned Folders**: Each version (V1, V2, etc.) contains numbered SQL files
2. **Ordered Execution**: Files are executed in alphabetical order within each version
3. **Idempotent Tracking**: `MigrationRunner` tracks which migrations have run
4. **History Table**: `__MigrationHistory` table stores execution details including:
   - Migration name
   - Version
   - Execution time
   - Success/failure status
   - Error messages (if failed)

## Creating a New Migration

### For V1 (Initial Schema):
Place new `.sql` files in `V1_Initial/` with a sequential number:
```
005_CreateUserConfig.sql
006_CreateAnotherTable.sql
```

### For V2+ (Future Versions):
1. Create a new folder: `V2_FeatureName/`
2. Add SQL files with sequential numbers starting from 001
3. Files must be embedded resources in the `.csproj` file

## Embedding SQL Files

Ensure your `.sql` files are embedded resources by adding to `BarcodeRevealTool.Persistence.csproj`:

```xml
<ItemGroup>
    <EmbeddedResource Include="Schema/Migrations/**/*.sql" />
</ItemGroup>
```

## Running Migrations

### Automatic (on application startup):
```csharp
var runner = new MigrationRunner(connectionString);
var result = await runner.RunAllMigrationsAsync();
if (!result.Success)
{
    throw new InvalidOperationException($"Migrations failed: {result.ErrorMessage}");
}
```

### Getting Migration History:
```csharp
var history = runner.GetMigrationHistory();
foreach (var entry in history)
{
    Console.WriteLine($"{entry.MigrationName}: {entry.Status} ({entry.ExecutionTimeMs}ms)");
}
```

## Migration Rules

1. **Always Use CREATE TABLE IF NOT EXISTS** - Prevents errors on re-runs
2. **Use IF NOT EXISTS for Indexes** - Prevents unique index violations
3. **Never Drop Tables** - Maintain history and data
4. **Update Incrementally** - Add columns with defaults, don't alter core structures
5. **Test Before Deploying** - Verify migrations work on fresh database

## Example Migration File

```sql
-- V2_AddNewFeature/001_AddFeatureTable.sql
CREATE TABLE IF NOT EXISTS FeatureTable (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FeatureName TEXT NOT NULL UNIQUE,
    IsEnabled BOOLEAN NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_feature_name ON FeatureTable(FeatureName);
```

## Troubleshooting

### Migration Failed Error
Check `__MigrationHistory` table for error details:
```sql
SELECT MigrationName, Status, ErrorMessage FROM __MigrationHistory WHERE Status = 'Failed';
```

### Stuck in Failed State
If a migration failed and you fixed the issue:
1. Delete the failed entry from `__MigrationHistory`
2. Fix the SQL in the migration file
3. Re-run the application

### Missing Files
Ensure `.sql` files are:
- In the correct version folder
- Embedded resources (check `.csproj` file)
- Named with sequential numbers (001_, 002_, etc.)
