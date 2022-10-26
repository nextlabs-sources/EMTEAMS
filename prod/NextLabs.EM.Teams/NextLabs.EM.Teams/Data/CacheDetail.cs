using Newtonsoft.Json;
using NextLabs.Teams.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NextLabs.Teams
{
	public enum CacheStatus
	{
		NULL,
		SYNCED,
		ADDED,
		UPDATED,
		DELETED
	}

	public class CacheDetail : ICloneable
	{
		public string Name { get; set; }

		public CacheStatus Status { get; set; }

		public TeamEnforce Enforce { get; set; }

		public Dictionary<string, List<string>> Tags;

		public CacheDetail()
		{
			Name = string.Empty;
			Enforce = TeamEnforce.Dont;
			Tags = new Dictionary<string, List<string>>();
			Status = CacheStatus.NULL;
		}

		public CacheDetail(string name, Dictionary<string, List<string>> dicTag, CacheStatus status, TeamEnforce enforce)
		{
			Name = name;
			Enforce = enforce;
			Tags = new Dictionary<string, List<string>>(dicTag);//dicTag;
			Status = status;
		}

		public void AddOrUpdate(Dictionary<string, List<string>> tags, TeamEnforce enforce, string name = null)
		{
			Enforce = enforce;
			if(name != null) Name = name;
			if (Tags.Count != 0)
			{
				foreach (var tag in tags)
				{
					if (Tags.ContainsKey(tag.Key)) Tags[tag.Key] = Tags[tag.Key].Union(tag.Value).ToList();
					else Tags[tag.Key] = tag.Value;
				}
			}
			else
			{
				Tags = tags;
			}
		}

		public object Clone()
		{
			Dictionary<string, List<string>> cloneTags = new Dictionary<string, List<string>>();

			foreach (var tag in Tags)
			{
				cloneTags.Add(tag.Key, new List<string>(tag.Value));
			}

			return new CacheDetail(this.Name, cloneTags, this.Status, this.Enforce);
		}
	}
}
