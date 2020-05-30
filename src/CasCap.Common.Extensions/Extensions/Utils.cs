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
    }
}