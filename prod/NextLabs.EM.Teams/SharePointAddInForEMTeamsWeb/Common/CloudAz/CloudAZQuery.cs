using System;
using System.Collections.Generic;
using System.Web.Configuration;
using SharePointAddInForEMTeamsWeb;

namespace QueryCloudAZSDK.CEModel
{
	public class CloudAZQuery
	{
		private static log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		private static string JPCHost;
		private static string OAUTHost;
		private static string ClientId;
		private static string ClientSecure;
		public static PolicyResult DefaultPCResult { get; private set; }
		private static CEQuery CEQuery;
		private static object syncRoot = new object();
		public static bool Inited { get; private set; } = false;

		private CloudAZQuery()
		{
			Init();
		}

		public static void Init()
		{
			lock (syncRoot)
			{
				JPCHost = WebConfigurationManager.AppSettings.Get("NextLabs:JPCHost");
				OAUTHost = WebConfigurationManager.AppSettings.Get("NextLabs:OAUTHost");
				ClientId = WebConfigurationManager.AppSettings.Get("NextLabs:ClientId");
				ClientSecure = WebConfigurationManager.AppSettings.Get("NextLabs:ClientSecure");
				DefaultPCResult = Trans2PolicyResult(WebConfigurationManager.AppSettings.Get("NextLabs:DefaultPCResult"));
				CEQuery = new CEQuery(JPCHost, OAUTHost, ClientId, ClientSecure);
				Inited = true;
			}
		}

		private static PolicyResult Trans2PolicyResult(string defaultResult) 
		{
			switch (defaultResult) 
			{
				case "0":
					return PolicyResult.Deny;
				case "1":
					return PolicyResult.Allow;
				case "2":
					return PolicyResult.DontCare;
				default:
					logger.Error("Unknown DefaultPCResult in Web.config, please check <add key=\"NextLabs: DefaultPCResult\" value=\"0\" /> line.");
					return PolicyResult.Deny;
			}
		}

		public static QueryStatus CheckConnection()
		{
			if (!Inited) throw new NotImplementedException("CloudAZQuery uninitialized!");
			CEAttres ceAttres = new CEAttres();
			ceAttres.AddAttribute(new CEAttribute("emteams", "getkeywords", CEAttributeType.XacmlString));
			CERequest ceReq = CreateQueryReq(TeamAction.Keywords_Query, string.Empty, "emteams1", ceAttres, "emteams2", "emteams3", new CEAttres());
			CEQuery.RefreshToken();
			QueryStatus emQueryRes = CEQuery.CheckResource(ceReq, out _, out _);
			return emQueryRes;
		}

		//NOTE: remoteAddress should set as string.Empty. When not, will response 'E_ResponseStatusAbnormal' status.
		public static CERequest CreateQueryReq(string strAction, string remoteAddress, string srcName,
				CEAttres ceSrcAttr, string userSid, string userName, CEAttres ceUserAttr)
		{
			if (!string.IsNullOrEmpty(strAction) && !string.IsNullOrEmpty(srcName))
			{
				CERequest obReq = new CERequest();

				// Action
				obReq.SetAction(strAction);

				// Host
				if (!string.IsNullOrEmpty(remoteAddress) && !remoteAddress.Contains(":")) //Not support IPV6
				{
					obReq.SetHost(remoteAddress, remoteAddress, null);
				}

				// Resource, MUST 
				ceSrcAttr.AddAttribute(new CEAttribute("emteams", srcName, CEAttributeType.XacmlString));
				obReq.SetSource(srcName, "emteams", ceSrcAttr);

				// User
				if (!string.IsNullOrEmpty(userName) || ceUserAttr != null)
				{
					obReq.SetUser(userSid, userName, ceUserAttr);
				}

				// App
				obReq.SetApp("emteams", null, null, null);

				// Environment: set Dont Care case.
				CEAttres envAttrs = new CEAttres();
				envAttrs.AddAttribute(new CEAttribute("dont-care-acceptable", "yes", CEAttributeType.XacmlString));
				obReq.SetEnvAttributes(envAttrs);

				return obReq;
			}
			return null;
		}

		public static QueryStatus QueryCloudAZPC(CERequest obReq, out List<CEObligation> listObligation, out PolicyResult emPolicyResult)
		{
			QueryStatus emQueryRes = CEQuery.CheckResource(obReq, out emPolicyResult, out listObligation);

			if (emQueryRes == QueryStatus.E_Unauthorized)
			{
				CEQuery.RefreshToken();
				emQueryRes = CEQuery.CheckResource(obReq, out emPolicyResult, out listObligation);
			}
			return emQueryRes;
		}

		public static QueryStatus MultipleQueryColuAZPC(List<CERequest> ceRequests, out List<List<CEObligation>> listObligations, out List<PolicyResult> listPolicyResults)
		{
			QueryStatus emQueryRes = CEQuery.CheckMultipleResources(ceRequests, out listPolicyResults, out listObligations);

			if (emQueryRes == QueryStatus.E_Unauthorized)
			{
				CEQuery.RefreshToken();
				emQueryRes = CEQuery.CheckMultipleResources(ceRequests, out listPolicyResults, out listObligations);
			}

			return emQueryRes;
		}
	}
}
