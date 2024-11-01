using Microsoft.VisualBasic.FileIO;
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
        /// ファイルのハッシュを得る
        /// </summary>
        /// <param name="filePath">対象となるファイルのフルパス</param>
        /// <returns>ハッシュ</returns>
        /// <remarks>ファイルが開けなかった場合は空配列を返す</remarks>
        static byte[] GetFileHash(string filePath)
        {
            using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
            try
            {
                using SHA256 sha256 = SHA256.Create();
                fs.Position = 0;
                byte[] hash = sha256.ComputeHash(fs);
                return hash;
            }
            catch (IOException e)
            {
                Console.WriteLine($"I/O Exception: {e.Message}");
                return Array.Empty<byte>();
            }
            catch (UnauthorizedAccessException e)
            {
                Console.WriteLine($"Access Exception: {e.Message}");
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// ディレクトリ内の全てのファイルのハッシュを計算し、ハッシュをキーとしたグループを返す
        /// </summary>
        /// <param name="directoryPath">対象となるディレクトリ</param>
        /// <returns>ハッシュをキーとしたグループ</returns>
        static IEnumerable<IGrouping<byte[], string>> GetHashGroups(string directoryPath)
        {

        }

        static void Main(string[] args)
        {
            bool deletesFiles = args.Contains(DeletesFilesCommand);
            string directoryPath = args.FirstOrDefault(s => s != DeletesFilesCommand) ?? AskDirectory();

            if (Directory.Exists(directoryPath))
            {
                var hashGroups = Directory.EnumerateFiles(directoryPath).AsParallel()
                    .Select(filePath => (filePath, hash: GetFileHash(filePath)))
                    .Where(tuple => tuple.hash.Length > 0).ToList()
                    .GroupBy(tuple => tuple.hash, tuple => tuple.filePath, new HashComparer());

                foreach (var group in hashGroups)
                {
                    if (group.Skip(1).Any())
                    {
                        Console.WriteLine($"Hash collision: {string.Join(", ", group.Select(filePath => Path.GetFileName(filePath)))}");
                        // "-r"指定がある場合は最も古いファイルを残して消去（ゴミ箱送り）する
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
                                        Console.WriteLine($"Deleted Duplicated File: {Path.GetFileName(filePath)}");
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
            else
            {
                Console.WriteLine("Directory is not found.");
            }

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
    }
}







