extern alias GraphBeta;
using System;
using QueryCloudAZSDK.CEModel;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Beta = GraphBeta.Microsoft.Graph;
using Newtonsoft.Json;

namespace SharePointAddInForEMTeamsWeb.Models
{
	public class FileAttr
	{
		public FileAttr(Beta.DriveItem item, bool isAttachment = false)
		{
			this.Id = item.Id;
			this.Name = item.Name;
			this.WebUrl = item.WebUrl;
			this.LastModifiedDateTime = item.LastModifiedDateTime;
			this.IsAttachment = isAttachment;
			this.LocalAttrs = new Dictionary<string, string>();
		}

		public string Id { get; set; }

		public string Name { get; set; }

		public string WebUrl { get; set; }

		public DateTimeOffset? LastModifiedDateTime { get; set; }

		public bool IsAttachment { get; set; }

		public Dictionary<string, string> LocalAttrs;

		public void InjectAttributesTo(ref CEAttres ceAttres)
		{
			ceAttres.AddAttribute(new CEAttribute("file_name", Name.ToLower(), CEAttributeType.XacmlString));
			ceAttres.AddAttribute(new CEAttribute("file_weburl", WebUrl.ToLower(), CEAttributeType.XacmlString));
			ceAttres.AddAttribute(new CEAttribute("file_lastmodifieddatetime", LastModifiedDateTime.ToString().ToLower(), CEAttributeType.XacmlString));
			ceAttres.AddAttribute(new CEAttribute("file_isattachment", IsAttachment.ToString().ToLower(), CEAttributeType.XacmlString));

			foreach (var kv in LocalAttrs)
			{
				if (!string.IsNullOrEmpty(kv.Value))
				{
					if (kv.Value.Contains(","))
					{
						foreach (var subValue in kv.Value.Split(','))
						{
							if (!string.IsNullOrEmpty(subValue)) ceAttres.AddAttribute(new CEAttribute($"{kv.Key.ToLower()}", subValue.ToLower(), CEAttributeType.XacmlString));
						}
					}
					else
						ceAttres.AddAttribute(new CEAttribute($"{kv.Key.ToLower()}", kv.Value.ToLower(), CEAttributeType.XacmlString));
				}
			}
		}
	}
}
