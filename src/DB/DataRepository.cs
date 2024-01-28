using Microsoft.Data.Sqlite;
using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace RPVoiceChat.DB
{
    public abstract class DataRepository : IDisposable
    {
        protected SqliteConnection connection { get; }
        private SQLiteDB _db;

        public DataRepository(ILogger logger, string dbName)
        {
            string dirPath = Path.Combine(GamePaths.DataPath, "ModData", RPVoiceChatMod.modID);
            Directory.CreateDirectory(dirPath);
            string filePath = Path.Combine(dirPath, dbName);
            _db = new SQLiteDB(logger);
            _db.OnCreateTables += CreateTablesIfNotExists;
            _db.Open(filePath);

            connection = _db.connection;
            DefineCommands();
        }

        protected abstract void CreateTablesIfNotExists(SqliteConnection connection);
        protected abstract void DefineCommands();

        public virtual void Dispose()
        {
            _db.Dispose();
        }
    }
}
