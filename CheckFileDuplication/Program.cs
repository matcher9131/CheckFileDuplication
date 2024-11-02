using Microsoft.VisualBasic.FileIO;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace CheckFileDuplication
{
    internal class Program
    {
        const string DeletesFilesCommand = "-r";

        /// <summary>
        /// 対象となるディレクトリを尋ね、入力されたものをstringで返す
        /// </summary>
        static string AskDirectory()
        {
            Console.Write("Input directory path: ");
            return Console.ReadLine()!.Trim('"');
        }

        /// <summary>
        /// 対象となるディレクトリに対して各ファイルのハッシュを計算し、ハッシュが衝突したファイルのパスを出力する
        /// </summary>
        /// <param name="directoryPath">対象となるディレクトリパス</param>
        /// <param name="deletesFiles">ハッシュの重複するファイルをゴミ箱に送るなら<see langword="true"/></param>
        static void Run(string directoryPath, bool deletesFiles)
        {
            var dic = Database.GetFileHashes(directoryPath);
            var updateDic = new ConcurrentDictionary<string, (byte[] hash, DateTime lastWriteTime)>();
            var insertDic = new ConcurrentDictionary<string, (byte[] hash, DateTime lastWriteTime)>();

            var hashGroups = Directory.EnumerateFiles(directoryPath)
                .AsParallel()
                .Select(filePath =>
                {
                    string filename = Path.GetFileName(filePath);
                    if (dic.TryGetValue(filename, out var tuple))
                    {
                        var lastWriteTime = File.GetLastWriteTime(filePath);
                        if (lastWriteTime == tuple.lastWriteTime)
                        {
                            return (filePath, tuple.hash);
                        }
                        else
                        {
                            // ファイルが更新されている
                            var hash = SHA256.HashData(File.ReadAllBytes(filePath));
                            updateDic.TryAdd(filename, (hash, lastWriteTime));
                            return (filePath, hash);
                        }
                    }
                    else
                    {
                        // DB内にデータがない
                        var hash = SHA256.HashData(File.ReadAllBytes(filePath));
                        var lastWriteTime = File.GetLastWriteTime(filePath);
                        insertDic.TryAdd(filename, (hash, lastWriteTime));
                        return (filePath, hash);
                    }
                })
                .Where(tuple => tuple.hash.Length > 0)
                .ToList()
                .GroupBy(tuple => tuple.hash, tuple => tuple.filePath, new HashComparer());

            Database.Update(directoryPath, updateDic);
            foreach (var filename in updateDic.Keys)
            {
                Console.WriteLine($"[Database] Updated: {filename}");
            }
            Database.Insert(directoryPath, insertDic);
            foreach (var filename in insertDic.Keys)
            {
                Console.WriteLine($"[Database] Inserted: {filename}");
            }

            foreach (var group in hashGroups)
            {
                if (group.Skip(1).Any())
                {
                    Console.WriteLine($"Hash collision: {string.Join(", ", group.Select(filePath => Path.GetFileName(filePath)))}");
                    // "-r"指定がある場合は最も古いファイル以外をゴミ箱に送る
                    if (deletesFiles)
                    {
                        var filePathLastWriteTimeTuples = group.Select(filePath => (filePath, lastWriteTime: File.GetLastWriteTime(filePath))).ToList();
                        DateTime oldestLastWriteTime = filePathLastWriteTimeTuples.Min(tuple => tuple.lastWriteTime);
                        foreach (var (filePath, lastWriteTime) in filePathLastWriteTimeTuples)
                        {
                            if (lastWriteTime > oldestLastWriteTime && File.Exists(filePath))
                            {
                                try
                                {
                                    FileSystem.DeleteFile(filePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                                    Console.WriteLine($"Deleted duplicated file: {Path.GetFileName(filePath)}");
                                }
                                catch (IOException e)
                                {
                                    Console.WriteLine($"I/O Exception: {e.Message}");
                                }
                                catch (UnauthorizedAccessException e)
                                {
                                    Console.WriteLine($"Access Exception: {e.Message}");
                                }
                            }
                        }
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            bool deletesFiles = args.Contains(DeletesFilesCommand);
            string directoryPath = args.FirstOrDefault(s => s != DeletesFilesCommand) ?? AskDirectory();
            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine("Directory is not found.");
            }

            if (Directory.Exists(directoryPath))
            {
                Run(directoryPath, deletesFiles);
            }
            else
            {
                Console.WriteLine("Directory is not found.");
            }

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
    }
}







