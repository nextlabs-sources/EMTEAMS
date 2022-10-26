// Copyright (c) NextLabs Corporation. All rights reserved.


namespace NextLabs.Teams.Models
{
	extern alias GraphBeta;
	using Beta = GraphBeta.Microsoft.Graph;
	using Newtonsoft.Json;
	using QueryCloudAZSDK.CEModel;
	using System.Collections.Generic;
	using System.ComponentModel.DataAnnotations.Schema;
	using System.ComponentModel.DataAnnotations;

    public enum TeamEnforce
	{
		Dont,
		Do
	}

	[Table("Team")]
	public class TeamAttr
	{
		public TeamAttr(string id)
		{
			this.Id = id;
			this.Name = string.Empty;
			this.DoEnforce = TeamEnforce.Dont;
			this.JsonClassifications ="{}";
		}

		public TeamAttr(string id, string name, bool initTag = true, TeamEnforce teamEnforce = TeamEnforce.Dont)
		{
			this.Id = id;
			this.Name = name;
			this.DoEnforce = teamEnforce;
			this.JsonClassifications = "{}";

			if (initTag) InitClassifications();
		}

		public TeamAttr(string id, string name, Dictionary<string, List<string>> tags, TeamEnforce teamEnforce = TeamEnforce.Dont)
		{
			this.Id = id;
			this.Name = name;
			this.DoEnforce = teamEnforce;
			this.Classifications = tags;
		}

		public TeamAttr(Beta.Group teamInfo, bool initTag = true, TeamEnforce teamEnforce = TeamEnforce.Dont) 
		{
			this.Id = teamInfo.Id;
			this.Name = teamInfo.DisplayName;
			this.DoEnforce = teamEnforce;
			this.JsonClassifications = "{}";

			if (initTag) InitClassifications();
		}
		[Key]
		public string Id { get; set; }
		public string Name { get; set; }
		public TeamEnforce DoEnforce { get; set; }
		public string JsonClassifications { get; set; }

		[NotMapped]
		public Dictionary<string, List<string>> Classifications
		{
			get
			{
				return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(JsonClassifications);
			}
			set
			{
				JsonClassifications = JsonConvert.SerializeObject(value, Formatting.None);
			}
		}

		private void InitClassifications()
		{
			if (TeamCache.TryGet(this.Id, out CacheDetail detail) && detail.Enforce == TeamEnforce.Do) 
			{
				this.Classifications = detail.Tags;
			}
		}

		//public void AddOrUpdateClassifications(Dictionary<string, List<string>> tags)
		//{
		//	Dictionary<string, List<string>> temp = Classifications;
		//	foreach (var tag in tags)
		//	{
		//		if (!temp.ContainsKey(tag.Key))
		//		{
		//			temp.Add(tag.Key, tag.Value);
		//		}
		//		else
		//		{
		//			foreach (var value in tag.Value)
		//			{
		//				if (!temp[tag.Key].Contains(value))
		//				{
		//					temp[tag.Key].Add(value);
		//				}
		//			}
		//		}
		//	}
		//	Classifications = temp;
		//}

		public void InjectAttributesTo(ref CEAttres ceAttres)
		{
			ceAttres.AddAttribute(new CEAttribute("team_name", Name.ToLower(), CEAttributeType.XacmlString));

			if (Classifications != null && Classifications.Count != 0)
			{
				foreach (var c in Classifications)
				{
					foreach (var v in c.Value)
					{
						ceAttres.AddAttribute(new CEAttribute($"teamtag_{c.Key.ToLower()}", v.ToLower(), CEAttributeType.XacmlString));
					}
				}
			}
		}
	}
}
