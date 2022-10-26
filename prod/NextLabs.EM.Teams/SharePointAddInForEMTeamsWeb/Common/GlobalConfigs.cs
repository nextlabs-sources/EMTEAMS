using System;
using System.IO;
using System.Web.Configuration;

namespace SharePointAddInForEMTeamsWeb
{
	public static class GlobalConfigs
	{
		public static string[] Keywords { get; private set; } = Array.Empty<string>();
		public static string TempFolder { get; private set; } = Path.GetTempPath();

		public static void SetKeywords(string[] arr)
		{
			lock (Keywords.SyncRoot)
			{
				Keywords = arr;
			}
		}

		public static string[] ParserKeywords()
		{
			string ContentKeywords = WebConfigurationManager.AppSettings.Get("NextLabs:ContentKeywords");
			if (!string.IsNullOrEmpty(ContentKeywords)) return ContentKeywords.Split(';');
			return new string[] { };
		}
	}

	public class TeamAction
	{
		public const string Channel_File_View = "CHANNEL_FILE_VIEW";
		public const string Channel_File_Upload = "CHANNEL_FILE_UPLOAD";
		public const string Keywords_Query = "KEYWORDS_QUERY";
	}


	public static class TeamObligation
	{
		public static class Keywords_Content_Analysis
		{
			public const string ObName = "keywords_content_analysis";
			public const string ObKey_keyword = "keyword";
		}
	}

	public class AttributePrefix
	{
		public const string File_ = "file_";
		public const string FileKeyword_ = "filekeyword_";
	}
}
