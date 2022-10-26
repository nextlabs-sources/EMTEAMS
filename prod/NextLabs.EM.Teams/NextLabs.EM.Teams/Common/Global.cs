// Copyright (c) NextLabs Corporation. All rights reserved.


namespace NextLabs.Common
{
	using System;
	using System.IO;

	public static class Global
	{
		public static string TempFolder { get; private set; } = Path.GetTempPath();
		public static string Shared_Documents = "/Shared%20Documents";
	}

	public class TeamAction
	{
		public const string Team_Create = "TEAM_CREATE";
		public const string Team_Join = "TEAM_JOIN";
		public const string Channel_File_View = "CHANNEL_FILE_VIEW";
		public const string Keywords_Query = "KEYWORDS_QUERY";
		public const string Team_Auto_Classify = "TEAM_AUTO_CLASSIFY";
	}


	public static class TeamObligation
	{
		public static class Team_Auto_Classify
		{
			public const string ObName = "team_auto_classify";
			public const string ObKey_key = "key";
			public const string ObKey_value = "value";
		}

		public static class Keywords_Content_Analysis
		{
			public const string ObName = "keywords_content_analysis";
			public const string ObKey_keyword = "keyword";
		}

		public static class Team_Notify
		{
			public const string ObName = "team_notify";
			public const string ObKey_message = "message";
		}
	}

	public class AttributePrefix
	{
		public const string Team_ = "team_";
		public const string TeamTag_ = "teamtag_";
		public const string File_ = "file_";
		public const string FileKeyword_ = "filekeyword_";
	}
}
