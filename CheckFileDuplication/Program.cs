using CheckFileDuplication;
using Microsoft.VisualBasic.FileIO;
using System.Security.Cryptography;

/// <summary>
/// 対象となるディレクトリを尋ね、入力されたものをstringで返す
/// </summary>
static string AskDirectory()
{
    Console.Write("Input directory path: ");
    return Console.ReadLine()!.Trim('"');
}

const string DeletesFilesCommand = "-r";
bool deletesFiles = args.Contains(DeletesFilesCommand);
string directoryPath = args.FirstOrDefault(s => s != DeletesFilesCommand) ?? AskDirectory();

if (Directory.Exists(directoryPath))
{
    var hashGroups = Directory.EnumerateFiles(directoryPath).AsParallel()
        .Select(filePath =>
            {
                // (ファイル名, hash)のタプルを作る
                using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
                try
                {
                    using SHA256 sha256 = SHA256.Create();
                    fs.Position = 0;
                    byte[] hash = sha256.ComputeHash(fs);
                    return (filePath, hash);
                }
                catch (IOException e)
                {
                    // ファイルが開けない場合はhashとして空配列を返し、あとでタプルごと除外する
                    Console.WriteLine($"I/O Exception: {e.Message}");
                    return (filePath, hash: Array.Empty<byte>());
                }
                catch (UnauthorizedAccessException e)
                {
                    // 同上
                    Console.WriteLine($"Access Exception: {e.Message}");
                    return (filePath, hash: Array.Empty<byte>());
                }
            }
        )
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