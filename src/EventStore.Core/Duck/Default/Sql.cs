namespace EventStore.Core.Duck.Default;

static class Sql {
    public const string AppendIndexSql = "insert or ignore into {table} (id, name) values ($id, $name);";
}
