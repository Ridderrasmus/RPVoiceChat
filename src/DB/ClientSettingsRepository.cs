using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using Vintagestory.API.Common;

namespace RPVoiceChat.DB
{
    public class ClientSettingsRepository : DataRepository
    {
        private const string dbName = "client.db";
        private const string tableName = "settings";
        private SQLiteCommand getSettingsCmd;
        private SQLiteCommand setSettingCmd;
        private Dictionary<string, float> cache;

        public ClientSettingsRepository(ILogger logger) : base(logger, dbName)
        {
            Load();
        }

        public float GetPlayerGain(string playerId)
        {
            if (cache.ContainsKey(playerId)) return cache[playerId];

            SetPlayerGain(playerId, 100);
            return cache[playerId];
        }

        public void SetPlayerGain(string playerId, int value)
        {
            float gain = (float)value / 100;
            cache[playerId] = gain;
        }

        public void Save()
        {
            using (SQLiteTransaction transaction = connection.BeginTransaction())
            {
                foreach (var entry in cache)
                {
                    setSettingCmd.Parameters["@playerId"].Value = entry.Key;
                    setSettingCmd.Parameters["@gain"].Value = entry.Value;
                    setSettingCmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        protected override void CreateTablesIfNotExists(SQLiteConnection connection)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = $"CREATE TABLE IF NOT EXISTS {tableName} (playerId TEXT PRIMARY KEY, gain REAL);";
                command.ExecuteNonQuery();
            }
        }

        protected override void DefineCommands()
        {
            getSettingsCmd = connection.CreateCommand();
            getSettingsCmd.CommandText = $"SELECT * FROM {tableName}";
            getSettingsCmd.Prepare();

            setSettingCmd = connection.CreateCommand();
            setSettingCmd.CommandText = $"INSERT OR REPLACE INTO {tableName} (playerId, gain) VALUES (@playerId, @gain)";
            setSettingCmd.Parameters.Add("@playerId", DbType.String, 64);
            setSettingCmd.Parameters.Add("@gain", DbType.Single);
            setSettingCmd.Prepare();
        }

        private void Load()
        {
            var entries = new Dictionary<string, float>();

            using (SQLiteDataReader datareader = getSettingsCmd.ExecuteReader())
            {
                while (datareader.Read())
                {
                    string playerId = datareader["playerId"].ToString();
                    float gain = datareader.GetFloat(1);
                    entries.Add(playerId, gain);
                }
            }

            cache = entries;
        }

        public override void Dispose()
        {
            getSettingsCmd?.Dispose();
            setSettingCmd?.Dispose();

            base.Dispose();
        }
    }
}
