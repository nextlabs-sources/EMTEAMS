// Copyright (c) NextLabs Corporation. All rights reserved.


namespace Microsoft.AspNetCore.Authentication
{
    /// <summary>
    /// Bot config
    /// </summary>
    public class BotOptions
    {
        /// <summary>
        /// Gets the bot app id
        /// </summary>
        public string MicrosoftAppId { get; set; }

        /// <summary>
        /// Gets the bot app password
        /// </summary>
        public string MicrosoftAppPassword { get; set; }

        /// <summary>
        /// The catalog app's generated app ID
        ///NOTE: CANNOT get it directly
        ///https://docs.microsoft.com/en-us/graph/api/resources/teamsapp?view=graph-rest-beta
        /// </summary>
        public string AppCatalogId { get; set; }

        public int RetryCount { get; set; }
        public int MinBackoff { get; set; }
        public int MaxBackoff { get; set; }
        public int DeltaBackoff { get; set; }
    }

}
