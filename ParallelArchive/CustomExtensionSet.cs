using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomExtensionSet
{
    public static class ExtensionSet
    {
        /// <summary>
        /// Compares string with an multiple strings and returns true if at least one match found.
        /// </summary>
        /// <param name="str">this string</param>
        /// <param name="compareTo">A series of strings to compare with</param>
        /// <returns></returns>
        public static bool MultipleComparsion(this string str, params string[] compareTo)
        {
            foreach(string probe in compareTo)
            {
                if (str == probe) return true;
            }
            return false;
        }
        /// <summary>
        /// Compares string with an multiple strings and returns true if at least one match found.
        /// </summary>
        /// <param name="str">this string</param>
        /// <param name="compareTo">A collection of strings to compare with</param>
        /// <returns></returns>
        public static bool MultipleComparsion(this string str, IEnumerable<string> compareTo)
        {
            return MultipleComparsion(str, compareTo);
        }
    }
}
