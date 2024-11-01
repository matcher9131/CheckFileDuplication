using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace CheckFileDuplication
{
    internal class Database : IDisposable
    {
        private const string TableName = "file";

        private readonly SqliteConnection connection;

        public Database()
        {
            this.connection = new SqliteConnection("Data Source=CheckFileDuplication.db");
            this.Initialize();
        }

        private void Initialize()
        {
            this.connection.Open();
            using var createTableCommand = this.connection.CreateCommand();
            createTableCommand.CommandText = $@"
                CREATE TABLE IF NOT EXISTS {TableName} (
                    path    BLOB NOT NULL PRIMARY KEY,
                    hash    BLOB NOT NULL,
                    date    TEXT NOT NULL
                );
            ";
            createTableCommand.ExecuteNonQuery();
        }

        public byte[] GetHashOrUpdate(string filePath)
        {
            var pathHash = SHA256.HashData(Encoding.UTF8.GetBytes(filePath));
            using var selectCommand = this.connection.CreateCommand();
            selectCommand.CommandText = $"SELECT hash, date FROM {TableName} WHERE path = $path;";
            selectCommand.Parameters.AddWithValue("$path", pathHash);
            using var reader = selectCommand.ExecuteReader();
            if (reader.HasRows)
            {
                reader.Read();
                var hash = reader.GetFieldValue<byte[]>(0);
                var date = reader.GetDateTime(1);
                var lastWriteTime = File.GetLastWriteTime(filePath);
                if (lastWriteTime > date)
                {
                    var newFileHash = HashUtil.GetFileHash(filePath);
                    if (newFileHash.Length == 0) return Array.Empty<byte>();
                    using var updateCommand = this.connection.CreateCommand();
                    updateCommand.CommandText = $"UPDATE {TableName} SET hash = $hash, date = $date WHERE path = $path;";
                    updateCommand.Parameters.AddWithValue("$hash", newFileHash);
                    updateCommand.Parameters.AddWithValue("$date", lastWriteTime);
                    updateCommand.Parameters.AddWithValue("$path", pathHash);
                    updateCommand.ExecuteNonQuery();
                    Console.WriteLine($"[Database] Updated: {Path.GetFileName(filePath)}");
                    return newFileHash;
                }
                else
                {
                    return hash;
                }
            }
            else
            {
                var newFileHash = HashUtil.GetFileHash(filePath);
                if (newFileHash.Length == 0) return Array.Empty<byte>();
                var lastWriteTime = File.GetLastWriteTime(filePath);
                using var insertCommand = this.connection.CreateCommand();
                insertCommand.CommandText = $"INSERT INTO {TableName} VALUES($path, $hash, $date);";
                insertCommand.Parameters.AddWithValue("$path", pathHash);
                insertCommand.Parameters.AddWithValue("$hash", newFileHash);
                insertCommand.Parameters.AddWithValue("$date", lastWriteTime);
                Console.WriteLine($"[Database] Inserted: {Path.GetFileName(filePath)}");
                return newFileHash;
            }
        }

        #region IDisposable
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)
                    this.connection.Dispose();
                }

                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // TODO: 大きなフィールドを null に設定します
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
