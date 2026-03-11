using Microsoft.Extensions.Logging;
using System.Reflection;

namespace CasCap.Common.Extensions;

/// <summary>
/// Extension methods for file and directory I/O operations.
/// </summary>
public static class IOExtensions
{
    private static readonly ILogger _logger = ApplicationLogging.CreateLogger(nameof(IOExtensions));

    /// <summary>
    /// Combines a root path with a relative folder or file path.
    /// </summary>
    /// <param name="root">The root directory path.</param>
    /// <param name="folderOrFile">The relative path to append.</param>
    /// <returns>The combined path.</returns>
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

    /// <summary>
    /// Writes all bytes to the specified path, creating the directory if it does not exist.
    /// </summary>
    /// <param name="path">The file path to write to.</param>
    /// <param name="bytes">The byte content to write.</param>
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

    /// <summary>
    /// Writes all text to the specified path, creating the directory if it does not exist.
    /// </summary>
    /// <param name="path">The file path to write to.</param>
    /// <param name="str">The string content to write.</param>
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

    /// <summary>
    /// Asynchronously writes all text to the specified path, creating the directory if it does not exist.
    /// </summary>
    /// <param name="path">The file path to write to.</param>
    /// <param name="str">The string content to write.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
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

    /// <summary>
    /// Appends text to the specified file, creating the file and directory if they do not exist.
    /// </summary>
    /// <param name="path">The file path to append to.</param>
    /// <param name="content">The content to append.</param>
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

    /// <summary>
    /// Reads all non-empty lines from the specified text file.
    /// </summary>
    /// <param name="path">The file path to read from.</param>
    /// <returns>A list of non-empty lines from the file.</returns>
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

    /// <summary>
    /// Recursively calculates the total size of all files in the specified folder.
    /// </summary>
    /// <param name="folder">The folder path to calculate the size of.</param>
    /// <returns>The total size in bytes.</returns>
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

    /// <summary>
    /// Deletes all files and subdirectories in the specified folder.
    /// </summary>
    /// <param name="folder">The folder path to clean.</param>
    /// <returns>A tuple containing the count of deleted files and directories.</returns>
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

    /// <summary>
    /// Loads a string from an embedded resource in the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly containing the embedded resource.</param>
    /// <param name="fileName">The fully qualified name of the embedded resource file.</param>
    /// <returns>The content of the embedded resource as a string, or an empty string if the resource is not found.</returns>
    public static string? GetManifestResourceString(this Assembly assembly, string fileName)
    {
        string? prompt = null;
        using (var stream = assembly.GetManifestResourceStream(fileName))
        {
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                prompt = reader.ReadToEnd();
            }
        }
        return prompt;
    }
}
