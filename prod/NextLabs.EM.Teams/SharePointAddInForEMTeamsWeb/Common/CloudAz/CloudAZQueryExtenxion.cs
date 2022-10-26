// Copyright (c) NextLabs Corporation. All rights reserved.


namespace QueryCloudAZSDK.CEModel
{
	using System;
	using System.Collections.Generic;

	public static class CloudAZQueryExtenxion
	{
		public static void InjectAttributesFrom(this CEAttres ceAttrs, IDictionary<string, string> dict) 
		{
			foreach (var kv in dict) 
			{
				if (!kv.Key.Equals("ODataType", StringComparison.OrdinalIgnoreCase))
				{
					if(kv.Key.Equals("mail", StringComparison.OrdinalIgnoreCase))
						ceAttrs.AddAttribute(new CEAttribute("email", kv.Value.ToLower(), CEAttributeType.XacmlString));
					else
						ceAttrs.AddAttribute(new CEAttribute(kv.Key.ToLower(), kv.Value.ToLower(), CEAttributeType.XacmlString));
				}
			}
		}
	}
}
