using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
namespace CasCap.Common.Extensions
{
    public class Utils
    {
        /// <summary>
        /// Gets all items for an enum value.
        /// </summary>
        public static IEnumerable<T> GetAllItems<T>() where T : Enum => (T[])Enum.GetValues(typeof(T));

        /// <summary>
        /// Does a Path.Combine() and creates the destination directory if not existing
        /// </summary>
        public static string GetLocalPath(string localPath, string subFolder)
        {
            subFolder = Path.Combine(localPath, subFolder);
            var directory = Path.GetDirectoryName(subFolder);
            if (!directories.Contains(directory) && !Directory.Exists(directory))
            {
                //Debugger.Break();
                Directory.CreateDirectory(directory);
                directories.Add(directory);
            }
            return subFolder;
        }

        static HashSet<string> directories { get; set; } = new HashSet<string>();

        /// <summary>
        /// Handy to find originating method name when debugging.
        /// </summary>
        public static string GetCallingMethodName([CallerMemberName] string caller = "") => caller;

        public static float CalculateFolderSize(string folder)
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
                        throw new Exception($"Unable to calculate folder size: {e.Message}");
                    }
                }
            }
            catch (UnauthorizedAccessException e)
            {
                throw new Exception($"Unable to calculate folder size: {e.Message}");
            }
            return folderSize;
        }

        public static (int files, int directories) Deltree(string folder)
        {
            var di = new DirectoryInfo(folder);
            var files = 0;
            foreach (var file in di.GetFiles())
            {
                file.Delete();
                files++;
            }
            var directories = 0;
            foreach (var dir in di.GetDirectories())
            {
                dir.Delete(true);
                directories++;
            }
            return (files, directories);
        }
    }
}