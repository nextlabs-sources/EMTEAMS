// Copyright (c) NextLabs Corporation. All rights reserved.


namespace QueryCloudAZSDK.CEModel
{
	using NextLabs.Common;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;

	public static class CloudAZQueryExtenxion
	{
		public static void ExtractTeamAutoClassify(this CEObligation ob, ref Dictionary<string, List<string>> tags)
		{
			string obName = ob.GetName();
			if (obName.StartsWith(TeamObligation.Team_Auto_Classify.ObName, StringComparison.OrdinalIgnoreCase))
			{
				if (tags == null) tags = new Dictionary<string, List<string>>();
				CEAttres ceAttres = ob.GetCEAttres();
				int count = ceAttres.Count;
				string attrName = null;
				string attrValue = null;
				for (int i = 0; i < count; ++i)
				{
					CEAttribute ceAttre = ceAttres[i];
					if (ceAttre.Name.Equals(TeamObligation.Team_Auto_Classify.ObKey_key, StringComparison.OrdinalIgnoreCase)) attrName = ceAttre.Value.ToLower();
					if (ceAttre.Name.Equals(TeamObligation.Team_Auto_Classify.ObKey_value, StringComparison.OrdinalIgnoreCase)) attrValue = ceAttre.Value.ToLower();
				}
				if (!string.IsNullOrEmpty(attrName) && !string.IsNullOrEmpty(attrValue))
				{
					if (!tags.ContainsKey(attrName)) tags[attrName] = new List<string>() { attrValue };
					else if (!tags[attrName].Contains(attrValue)) tags[attrName].Add(attrValue);
				}
			}
		}

		public static bool ExtractTeamNotify(this CEObligation ob, out string notify)
		{
			bool result = false;
			notify = null;
			string obName = ob.GetName();
			if (obName.StartsWith(TeamObligation.Team_Notify.ObName, StringComparison.OrdinalIgnoreCase))
			{
				CEAttres ceAttres = ob.GetCEAttres();
				int count = ceAttres.Count;
				for (int i = 0; i < count; ++i)
				{
					CEAttribute ceAttre = ceAttres[i];
					if (ceAttre.Name.Equals(TeamObligation.Team_Notify.ObKey_message, StringComparison.OrdinalIgnoreCase))
					{
						notify = ceAttre.Value;
						result = true;
						break;
					}
				}
			}
			return result;
		}

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
