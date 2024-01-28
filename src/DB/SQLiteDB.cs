using System;
using Microsoft.Data.Sqlite;
using Vintagestory.API.Common;

namespace RPVoiceChat.DB
{
    internal sealed class SQLiteDB : SQLiteDBConnection
    {

        public SqliteConnection connection { get => sqliteConn; }
        public event Action<SqliteConnection> OnCreateTables;

        public SQLiteDB(ILogger logger) : base(logger) { }

        public void Open(string filePath)
        {
            string error = null;
            bool success = OpenOrCreate(filePath, ref error, true, true, false);
            if (!success) throw new Exception($"Failed to create data repository: {error}");
        }

        protected override void CreateTablesIfNotExists(SqliteConnection connection)
        {
            OnCreateTables?.Invoke(connection);
        }
    }
}
