using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace CheckFileDuplication
{
    internal static class Database
    {
        private const string ConnectionString = "Data Source=CheckFileDuplication.db";
        private const string TableName = "file";
        private const string ColumnDirectory = "directory";
        private const string ColumnFilename = "filename";
        private const string ColumnHash = "hash";
        private const string ColumnLastModified = "last_modified";

        static Database()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = $@"
                CREATE TABLE IF NOT EXISTS {TableName} (
                    {ColumnDirectory}       BLOB NOT NULL,
                    {ColumnFilename}        TEXT NOT NULL,
                    {ColumnHash}            BLOB NOT NULL,
                    {ColumnLastModified}    TEXT NOT NULL,
                    PRIMARY KEY({ColumnDirectory}, {ColumnFilename})
                );
            ";
            createTableCommand.ExecuteNonQuery();
        }

        /// <summary>
        /// 指定したディレクトリ内の全てのファイルに関してデータベースに保存されているハッシュと更新日時を取得する
        /// </summary>
        /// <param name="directoryPath">対象となるディレクトリのパス</param>
        /// <returns>ファイル名をキー、ハッシュと更新日時のタプルを値とする辞書</returns>
        public static Dictionary<string, (byte[] hash, DateTime lastWriteTime)> GetFileHashes(string directoryPath)
        {
            var directoryHash = SHA256.HashData(Encoding.UTF8.GetBytes(directoryPath));
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = $@"SELECT {ColumnFilename}, {ColumnHash}, {ColumnLastModified} FROM {TableName} WHERE {ColumnDirectory} = @directoryHash;";
            selectCommand.Parameters.AddWithValue("@directoryHash", directoryHash);
            using var reader = selectCommand.ExecuteReader();
            Dictionary<string, (byte[] hash, DateTime lastWriteTime)> dic = new();
            while (reader.Read())
            {
                var filename = reader.GetString(0);
                var hash = reader.GetFieldValue<byte[]>(1);
                var lastWriteTime = reader.GetDateTime(2);
                dic[filename] = (hash, lastWriteTime);
            }
            return dic;
        }

        /// <summary>
        /// データベースに存在するファイルのハッシュと更新日時を更新する
        /// </summary>
        /// <param name="directoryPath">対象となるディレクトリのパス</param>
        /// <param name="dic">対象となるファイル名をキー、ハッシュと更新日時のタプルを値とする辞書</param>
        public static void Update(string directoryPath, ConcurrentDictionary<string, (byte[] hash, DateTime lastWriteTime)> dic)
        {
            var directoryHash = SHA256.HashData(Encoding.UTF8.GetBytes(directoryPath));
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            foreach (var (filename, (hash, lastWriteTime)) in dic)
            {
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = $@"UPDATE {TableName} SET {ColumnHash} = @hash, {ColumnLastModified} = @lastWriteTime WHERE {ColumnDirectory} = @directoryHash AND {ColumnFilename} = @filename;";
                updateCommand.Parameters.AddWithValue("@hash", hash);
                updateCommand.Parameters.AddWithValue("@lastWriteTime", lastWriteTime);
                updateCommand.Parameters.AddWithValue("@directoryHash", directoryHash);
                updateCommand.Parameters.AddWithValue("@filename", filename);
                updateCommand.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        /// <summary>
        /// データベースにファイルのハッシュと更新日時を追加する
        /// </summary>
        /// <param name="directoryPath">対象となるディレクトリのパス</param>
        /// <param name="dic">対象となるファイル名をキー、ハッシュと更新日時のタプルを値とする辞書</param>
        public static void Insert(string directoryPath, ConcurrentDictionary<string, (byte[] hash, DateTime lastWriteTime)> dic)
        {
            var directoryHash = SHA256.HashData(Encoding.UTF8.GetBytes(directoryPath));
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            foreach (var (filename, (hash, lastWriteTime)) in dic)
            {
                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = $@"INSERT INTO {TableName} VALUES(@directoryHash, @filename, @hash, @lastWriteTime);";
                insertCommand.Parameters.AddWithValue("@directoryHash", directoryHash);
                insertCommand.Parameters.AddWithValue("@filename", filename);
                insertCommand.Parameters.AddWithValue("@hash", hash);
                insertCommand.Parameters.AddWithValue("@lastWriteTime", lastWriteTime);
                insertCommand.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }
}
