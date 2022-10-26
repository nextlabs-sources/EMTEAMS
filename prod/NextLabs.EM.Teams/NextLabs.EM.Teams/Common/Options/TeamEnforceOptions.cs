using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NextLabs.Common
{
    public class TeamEnforceOptions
    {
        /// <summary>
        /// The catalog app's generated app ID
        ///NOTE: CANNOT get it directly
        ///https://docs.microsoft.com/en-us/graph/api/resources/teamsapp?view=graph-rest-beta
        /// </summary>
        public string AppCatalogId { get; set; }

        /// <summary>
        /// Team Scan Interval for team creation
        /// </summary>
        public int TeamScanInterval { get; set; }
    }
}
