using Microsoft.Extensions.Logging;

namespace CasCap.Common.Extensions;

public static class IOExtensions
{
    private static readonly ILogger _logger = ApplicationLogging.CreateLogger(nameof(IOExtensions));

    public static string Extend(this string root, string folderOrFile)
    {
        var path = Path.Combine(root, folderOrFile);
        return path;
    }

    //public static string ExtendAndCreateDirectory(this string root, string folderOrFile)
    //{
    //    var directory = root.ExtendPath(Path.GetFullPath(folderOrFile));
    //    if (!Directory.Exists(directory))
    //        Directory.CreateDirectory(directory);
    //    return directory;
    //}

    public static void WriteAllBytes(this string path, byte[] bytes)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir is not null)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, bytes);
        }
        else
            throw new GenericException($"GetDirectoryName not possible for path '{path}'");
    }

    [Obsolete("Switch to WriteAllTextAsync")]
    public static void WriteAllText(this string path, string str)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir is not null)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, str);
        }
        else
            throw new GenericException($"GetDirectoryName not possible for path '{path}'");
    }

    public async static Task WriteAllTextAsync(this string path, string str, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir is not null)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
#if NET8_0_OR_GREATER
            await File.WriteAllTextAsync(path, str, cancellationToken);
#else
            await Task.Delay(0, cancellationToken);
            File.WriteAllText(path, str);
#endif
        }
        else
            throw new GenericException($"GetDirectoryName not possible for path '{path}'");
    }

    public static void AppendTextFile(this string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir is not null)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (!File.Exists(path))
            {
                using var sw = File.CreateText(path);
                sw.WriteLine(content);
            }
            else
            {
                try
                {
                    using var sw = File.AppendText(path);
                    sw.WriteLine(content);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{ClassName} append failed", nameof(IOExtensions));
                    throw;
                }
            }
        }
        else
            throw new GenericException($"GetDirectoryName not possible for path '{path}'");
    }

    public static List<string> ReadTextFile(this string path)
    {
        var output = new List<string>(50000);
        if (File.Exists(path))
        {
            //var count = TotalLines(path);
            //output = new List<string>(count);
            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                output.Add(line);
            }
            //using (var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            //{
            //    using (var stream = new StreamReader(fs))
            //    {
            //        while (true)
            //        {
            //            var line = stream.ReadLine();
            //            output.Add(line);
            //            if (line is null)
            //                break;
            //        }
            //    }
            //}
        }
        return output;
    }

    /// <summary>
    /// Does a Path.Combine() and creates the destination directory if not existing
    /// </summary>
    public static string GetLocalPath(this string basePath, string relativePath)
    {
        var path = basePath.Extend(relativePath);
        var dirName = Path.GetDirectoryName(path);
        if (dirName is not null && !directories.Contains(dirName) && !Directory.Exists(dirName))
        {
            //Debugger.Break();
            Directory.CreateDirectory(dirName);
            directories.Add(dirName);
        }
        return path;
    }

    private static HashSet<string> directories { get; set; } = [];

    public static float CalculateFolderSize(this string folder)
    {
        var folderSize = 0.0f;
        try
        {
            if (!Directory.Exists(folder))
                return folderSize;
            else
            {
                try
                {
                    foreach (var file in Directory.GetFiles(folder))
                    {
                        if (File.Exists(file))
                        {
                            var finfo = new FileInfo(file);
                            folderSize += finfo.Length;
                        }
                    }
                    foreach (var dir in Directory.GetDirectories(folder))
                        folderSize += CalculateFolderSize(dir);
                }
                catch (NotSupportedException e)
                {
                    throw new GenericException($"Unable to calculate folder size: {e.Message}");
                }
            }
        }
        catch (UnauthorizedAccessException e)
        {
            throw new GenericException($"Unable to calculate folder size: {e.Message}");
        }
        return folderSize;
    }

    public static (int files, int directories) Deltree(this string folder)
    {
        var di = new DirectoryInfo(folder);
        var files = 0;
        foreach (var file in di.GetFiles())
        {
            file.Delete();
            files++;
        }
        var directoryCount = 0;
        foreach (var dir in di.GetDirectories())
        {
            dir.Delete(true);
            directoryCount++;
        }
        return (files, directoryCount);
    }
}
