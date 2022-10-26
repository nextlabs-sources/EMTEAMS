extern alias GraphBeta;
using Beta = GraphBeta.Microsoft.Graph;
using Newtonsoft.Json;
using NextLabs.GraphApp;
using QueryCloudAZSDK.CEModel;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;
using NextLabs.Data;
using System;
using System.ComponentModel.DataAnnotations;

namespace SharePointAddInForEMTeamsWeb.Models
{
	public enum TeamEnforce
	{
		Dont,
		Do
	}

	public class TeamAttr
	{
		public TeamAttr()
		{
			this.Id = string.Empty;
			this.Name = string.Empty;
			this.DoEnforce = TeamEnforce.Dont;
			this.JsonClassifications = "{}";
		}

		public TeamAttr(string id)
		{
			this.Id = id;
			this.Name = string.Empty;
			this.DoEnforce = TeamEnforce.Dont;
			this.JsonClassifications = "{}";
		}

		public TeamAttr(string id, string name, bool initTag = true, TeamEnforce teamEnforce = TeamEnforce.Dont)
		{
			this.Id = id;
			this.Name = name;
			this.DoEnforce = teamEnforce;
			this.JsonClassifications = "{}";

			if (initTag) InitClassifications();
		}

		public TeamAttr(Beta.Group teamInfo, bool initTag = true, TeamEnforce teamEnforce = TeamEnforce.Dont)
		{
			this.Id = teamInfo.Id;
			this.Name = teamInfo.DisplayName;
			this.DoEnforce = teamEnforce;
			this.JsonClassifications = "{}";

			if (initTag) InitClassifications();
		}
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

		private void InitClassifications() 
		{
			using (var ctx = new NxlDBContext())
			{
				var teamAttrs = ctx.TeamAttrs.Find(this.Id);
				if (teamAttrs != null && teamAttrs.DoEnforce == TeamEnforce.Do)
					this.JsonClassifications = teamAttrs.JsonClassifications;
			}
		}
	}
}
