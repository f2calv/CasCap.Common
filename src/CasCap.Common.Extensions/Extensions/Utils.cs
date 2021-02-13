﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
namespace CasCap.Common.Extensions
{
    public class Utils
    {
        /// <summary>
        /// Gets all items for an enum value.
        /// </summary>
        public static IEnumerable<TENum> GetAllItems<TENum>() where TENum : Enum => (TENum[])Enum.GetValues(typeof(TENum));

        public static IEnumerable<TEnum> GetAllCombinations<TEnum>() where TEnum : Enum
        {
            var highestEnum = Enum.GetValues(typeof(TEnum)).Cast<int>().Max();
            var upperBound = highestEnum * 2;
            for (var x = 0; x < upperBound; x++)
            {
                var value = (TEnum)(object)x;
                //l.Add(value);
                yield return value;
            }
        }

        /// <summary>
        /// Does a Path.Combine() and creates the destination directory if not existing
        /// </summary>
        public static string GetLocalPath(string localPath, string subFolder)
        {
            subFolder = Path.Combine(localPath, subFolder);
            var dir = Path.GetDirectoryName(subFolder);
            if (dir is object && !directories.Contains(dir) && !Directory.Exists(dir))
            {
                //Debugger.Break();
                Directory.CreateDirectory(dir);
                directories.Add(dir);
            }
            return subFolder;
        }

        static HashSet<string> directories { get; set; } = new();

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