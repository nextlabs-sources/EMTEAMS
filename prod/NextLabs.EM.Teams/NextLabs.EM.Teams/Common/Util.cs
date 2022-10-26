// Copyright (c) NextLabs Corporation. All rights reserved.


namespace NextLabs.Common
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

	public static class Util
	{
		public static string ToDisplayString(this Dictionary<string, List<string>> dict)
		{
			string result = "";
			foreach (var elem in dict)
			{
				result += $"{elem.Key}={string.Join(",", elem.Value)};";
			}
			return result.TrimEnd(';');
		}

		public static bool IsNull(this object obj)
		{
			return obj == null ? true : false;
		}

		public static bool NotNull(this object obj)
		{
			return obj == null ? false : true;
		}

		public static bool CheckResponseStatusCodeFailed(Microsoft.Rest.HttpOperationException hoe)
		{
			var statusCode = hoe.Response.StatusCode;
			var method = hoe.Request.Method;
			if (statusCode == HttpStatusCode.OK || statusCode == HttpStatusCode.Accepted ||
				(statusCode == HttpStatusCode.Created && method == HttpMethod.Put) ||
				(statusCode == HttpStatusCode.NoContent && (method == HttpMethod.Delete || method == HttpMethod.Post)))
			{
				return false;
			}
			return true;
		}
	}
}
