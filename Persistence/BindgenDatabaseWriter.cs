using Microsoft.Data.Sqlite;
using WinmnDump.Generation;

namespace WinmnDump.Persistence;

public sealed class BindgenDatabaseWriter
{
    private const int SchemaVersion = 1;

    public long WriteRun(string databasePath, BindgenRunContext context, GeneratedBinding binding)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath)) ?? ".");

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Pooling = false
        }.ToString());
        connection.Open();

        EnsureSchema(connection);

        using var transaction = connection.BeginTransaction();
        var runId = InsertRun(connection, transaction, context, binding);
        InsertLinkLibraries(connection, transaction, runId, binding);
        InsertTypes(connection, transaction, runId, binding);
        InsertFunctions(connection, transaction, runId, binding);
        InsertConstants(connection, transaction, runId, binding);
        InsertWarnings(connection, transaction, runId, binding);
        transaction.Commit();
        return runId;
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        var version = Convert.ToInt32(Scalar(connection, null, "PRAGMA user_version;"));
        if (version > SchemaVersion)
            throw new InvalidOperationException($"Unsupported bindgen database schema version {version}.");

        Execute(connection, null, """
            CREATE TABLE IF NOT EXISTS runs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                created_utc TEXT NOT NULL,
                winmd_path TEXT NOT NULL,
                subset_path TEXT NOT NULL,
                output_path TEXT NOT NULL,
                module_name TEXT NOT NULL,
                type_count INTEGER NOT NULL,
                function_count INTEGER NOT NULL,
                constant_count INTEGER NOT NULL,
                warning_count INTEGER NOT NULL,
                generator_version TEXT NOT NULL,
                subset_json TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS link_libraries (
                run_id INTEGER NOT NULL,
                library TEXT NOT NULL,
                source_import_modules TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS types (
                run_id INTEGER NOT NULL,
                original_name TEXT NOT NULL,
                c3_name TEXT NOT NULL,
                namespace TEXT NOT NULL,
                kind TEXT NOT NULL,
                abi_type TEXT,
                alias_target TEXT,
                c3_decl_kind TEXT NOT NULL,
                emitted INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS type_fields (
                run_id INTEGER NOT NULL,
                type_original_name TEXT NOT NULL,
                ordinal INTEGER NOT NULL,
                original_name TEXT NOT NULL,
                c3_name TEXT NOT NULL,
                original_type TEXT NOT NULL,
                c3_type TEXT NOT NULL,
                literal_value TEXT,
                emitted INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS functions (
                run_id INTEGER NOT NULL,
                original_name TEXT NOT NULL,
                c3_name TEXT NOT NULL,
                namespace TEXT NOT NULL,
                return_type TEXT NOT NULL,
                c3_return_type TEXT NOT NULL,
                import_name TEXT,
                import_module TEXT,
                emitted INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS function_parameters (
                run_id INTEGER NOT NULL,
                function_original_name TEXT NOT NULL,
                ordinal INTEGER NOT NULL,
                original_name TEXT NOT NULL,
                c3_name TEXT NOT NULL,
                original_type TEXT NOT NULL,
                c3_type TEXT NOT NULL,
                direction TEXT NOT NULL,
                non_null INTEGER NOT NULL,
                contract_annotation TEXT
            );

            CREATE TABLE IF NOT EXISTS constants (
                run_id INTEGER NOT NULL,
                original_name TEXT NOT NULL,
                c3_name TEXT NOT NULL,
                namespace TEXT NOT NULL,
                original_type TEXT NOT NULL,
                c3_type TEXT NOT NULL,
                value TEXT NOT NULL,
                emitted_value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS warnings (
                run_id INTEGER NOT NULL,
                message TEXT NOT NULL
            );
            """);

        Execute(connection, null, $"PRAGMA user_version = {SchemaVersion};");
    }

    private static long InsertRun(
        SqliteConnection connection,
        SqliteTransaction transaction,
        BindgenRunContext context,
        GeneratedBinding binding)
    {
        Execute(connection, transaction, """
            INSERT INTO runs (
                created_utc, winmd_path, subset_path, output_path, module_name,
                type_count, function_count, constant_count, warning_count,
                generator_version, subset_json
            ) VALUES (
                $created_utc, $winmd_path, $subset_path, $output_path, $module_name,
                $type_count, $function_count, $constant_count, $warning_count,
                $generator_version, $subset_json
            );
            """,
            ("$created_utc", DateTimeOffset.UtcNow.ToString("O")),
            ("$winmd_path", context.WinmdPath),
            ("$subset_path", context.SubsetPath),
            ("$output_path", context.OutputPath),
            ("$module_name", binding.ModuleName),
            ("$type_count", binding.Types.Count),
            ("$function_count", binding.Functions.Count),
            ("$constant_count", binding.Constants.Count),
            ("$warning_count", binding.Warnings.Count),
            ("$generator_version", context.GeneratorVersion),
            ("$subset_json", context.SubsetJson));

        return Convert.ToInt64(Scalar(connection, transaction, "SELECT last_insert_rowid();"));
    }

    private static void InsertLinkLibraries(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long runId,
        GeneratedBinding binding)
    {
        foreach (var library in binding.LinkLibraries)
        {
            Execute(connection, transaction,
                "INSERT INTO link_libraries (run_id, library, source_import_modules) VALUES ($run_id, $library, $source_import_modules);",
                ("$run_id", runId),
                ("$library", library.Library),
                ("$source_import_modules", string.Join(";", library.SourceImportModules)));
        }
    }

    private static void InsertTypes(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long runId,
        GeneratedBinding binding)
    {
        foreach (var type in binding.Types)
        {
            Execute(connection, transaction, """
                INSERT INTO types (
                    run_id, original_name, c3_name, namespace, kind, abi_type,
                    alias_target, c3_decl_kind, emitted
                ) VALUES (
                    $run_id, $original_name, $c3_name, $namespace, $kind, $abi_type,
                    $alias_target, $c3_decl_kind, $emitted
                );
                """,
                ("$run_id", runId),
                ("$original_name", type.OriginalName),
                ("$c3_name", type.C3Name),
                ("$namespace", type.Namespace),
                ("$kind", type.Kind.ToString()),
                ("$abi_type", type.AbiType),
                ("$alias_target", type.AliasTarget),
                ("$c3_decl_kind", type.C3DeclKind),
                ("$emitted", type.Emitted ? 1 : 0));

            foreach (var field in type.Fields)
            {
                Execute(connection, transaction, """
                    INSERT INTO type_fields (
                        run_id, type_original_name, ordinal, original_name, c3_name,
                        original_type, c3_type, literal_value, emitted
                    ) VALUES (
                        $run_id, $type_original_name, $ordinal, $original_name, $c3_name,
                        $original_type, $c3_type, $literal_value, $emitted
                    );
                    """,
                    ("$run_id", runId),
                    ("$type_original_name", type.OriginalName),
                    ("$ordinal", field.Ordinal),
                    ("$original_name", field.OriginalName),
                    ("$c3_name", field.C3Name),
                    ("$original_type", field.OriginalType),
                    ("$c3_type", field.C3Type),
                    ("$literal_value", field.LiteralValue),
                    ("$emitted", field.Emitted ? 1 : 0));
            }
        }
    }

    private static void InsertFunctions(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long runId,
        GeneratedBinding binding)
    {
        foreach (var fn in binding.Functions)
        {
            Execute(connection, transaction, """
                INSERT INTO functions (
                    run_id, original_name, c3_name, namespace, return_type, c3_return_type,
                    import_name, import_module, emitted
                ) VALUES (
                    $run_id, $original_name, $c3_name, $namespace, $return_type, $c3_return_type,
                    $import_name, $import_module, $emitted
                );
                """,
                ("$run_id", runId),
                ("$original_name", fn.OriginalName),
                ("$c3_name", fn.C3Name),
                ("$namespace", fn.Namespace),
                ("$return_type", fn.ReturnType),
                ("$c3_return_type", fn.C3ReturnType),
                ("$import_name", fn.ImportName),
                ("$import_module", fn.ImportModule),
                ("$emitted", fn.Emitted ? 1 : 0));

            foreach (var parameter in fn.Parameters)
            {
                Execute(connection, transaction, """
                    INSERT INTO function_parameters (
                        run_id, function_original_name, ordinal, original_name, c3_name,
                        original_type, c3_type, direction, non_null, contract_annotation
                    ) VALUES (
                        $run_id, $function_original_name, $ordinal, $original_name, $c3_name,
                        $original_type, $c3_type, $direction, $non_null, $contract_annotation
                    );
                    """,
                    ("$run_id", runId),
                    ("$function_original_name", fn.OriginalName),
                    ("$ordinal", parameter.Ordinal),
                    ("$original_name", parameter.OriginalName),
                    ("$c3_name", parameter.C3Name),
                    ("$original_type", parameter.OriginalType),
                    ("$c3_type", parameter.C3Type),
                    ("$direction", parameter.Direction.ToString()),
                    ("$non_null", parameter.NonNull ? 1 : 0),
                    ("$contract_annotation", parameter.ContractAnnotation));
            }
        }
    }

    private static void InsertConstants(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long runId,
        GeneratedBinding binding)
    {
        foreach (var constant in binding.Constants)
        {
            Execute(connection, transaction, """
                INSERT INTO constants (
                    run_id, original_name, c3_name, namespace, original_type, c3_type,
                    value, emitted_value
                ) VALUES (
                    $run_id, $original_name, $c3_name, $namespace, $original_type, $c3_type,
                    $value, $emitted_value
                );
                """,
                ("$run_id", runId),
                ("$original_name", constant.OriginalName),
                ("$c3_name", constant.C3Name),
                ("$namespace", constant.Namespace),
                ("$original_type", constant.OriginalType),
                ("$c3_type", constant.C3Type),
                ("$value", constant.Value),
                ("$emitted_value", constant.EmittedValue));
        }
    }

    private static void InsertWarnings(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long runId,
        GeneratedBinding binding)
    {
        foreach (var warning in binding.Warnings)
        {
            Execute(connection, transaction,
                "INSERT INTO warnings (run_id, message) VALUES ($run_id, $message);",
                ("$run_id", runId),
                ("$message", warning));
        }
    }

    private static object? Scalar(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var command = CreateCommand(connection, transaction, sql, parameters);
        return command.ExecuteScalar();
    }

    private static void Execute(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var command = CreateCommand(connection, transaction, sql, parameters);
        command.ExecuteNonQuery();
    }

    private static SqliteCommand CreateCommand(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);

        return command;
    }
}

public sealed class BindgenRunContext
{
    public required string WinmdPath { get; init; }
    public required string SubsetPath { get; init; }
    public required string OutputPath { get; init; }
    public required string SubsetJson { get; init; }
    public string GeneratorVersion { get; init; } = "1";
}
