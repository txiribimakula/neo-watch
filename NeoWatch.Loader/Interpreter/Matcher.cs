using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NeoWatch.Loading
{
    public static class Matcher
    {
        public static Match GetMatch(string value, string[] patterns)
        {
            Match match = Match.Empty;

            foreach (var pattern in patterns)
            {
                match = Regex.Match(value, pattern);
                if (match.Success)
                {
                    return match;
                }
            }

            return match;
        }
    }
}
