// Copyright (c) NextLabs Corporation. All rights reserved.


using QueryCloudAZSDK.CEModel;
using System.Collections.Generic;

namespace NextLabs.Common
{
    public class GeneralSettingOptions
    {
        public string PCHost { get; set; }
        public string PCId { get; set; }
        public string PCKey { get; set; }
        public string CCHost { get; set; }
        public PolicyResult DefaultPCResult { get; set; }
    }
}
