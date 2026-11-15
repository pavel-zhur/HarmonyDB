using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations.Internal;

namespace OneShelf.Common.Database;

#pragma warning disable EF1001 // the provider's history repository is internal API

/// <summary>
/// Npgsql 8 looks for the migrations history table without a schema in "public". The contexts set no schema: their table
/// is created in the schema of the role's search_path, so that is where it is looked for, or a second migration fails.
/// </summary>
internal class SearchPathHistoryRepository(HistoryRepositoryDependencies dependencies) : NpgsqlHistoryRepository(dependencies)
{
    protected override string ExistsSql
        => TableSchema != null
            ? base.ExistsSql
            : $"""
               SELECT EXISTS (
                   SELECT 1 FROM pg_catalog.pg_class c
                   JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
                   WHERE n.nspname=current_schema() AND
                         c.relname={Dependencies.TypeMappingSource.GetMapping(typeof(string)).GenerateSqlLiteral(TableName)}
               )
               """;
}
