// Copyright (c) NextLabs Corporation. All rights reserved.


namespace QueryCloudAZSDK.CEModel
{
	using System;
	using System.Collections.Generic;
	using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NextLabs.Common;
	using NextLabs.Teams.Models;
	using QueryCloudAZSDK;
	using QueryCloudAZSDK.CEModel;

	public class CloudAZQuery
	{
		private readonly ILogger logger;
		IOptionsMonitor<GeneralSettingOptions> generalSetting;
		private string JPCHost { get { return generalSetting.CurrentValue.PCHost; } }
		private string OAUTHost { get { return generalSetting.CurrentValue.CCHost; } }
		private string ClientId { get { return generalSetting.CurrentValue.PCId; } }
		private string ClientSecure { get { return generalSetting.CurrentValue.PCKey; } }
		public PolicyResult DefaultPCResult { get; private set; }
		private object changeLock = new object();
		private CEQuery CEQuery;

		private const int MAXRETRY = 3;

		public CloudAZQuery(IOptionsMonitor<GeneralSettingOptions> generalSetting, ILogger<CloudAZQuery> logger)
		{
			this.logger = logger ?? throw new ArgumentNullException(nameof(this.logger));
			this.generalSetting = generalSetting ?? throw new ArgumentNullException(nameof(this.generalSetting));
			this.generalSetting.OnChange(generalSetting => {
				lock (changeLock)
				{
					this.DefaultPCResult = generalSetting.DefaultPCResult;
					this.CEQuery = new CEQuery(JPCHost, OAUTHost, ClientId, ClientSecure) ?? throw new ArgumentNullException(nameof(this.CEQuery));
				}
				this.logger.LogInformation("GeneralSettingOptions of CloudAZQuery Changed, DefaultPCResult: {defaultPCResult}, Connection: {connected}", this.DefaultPCResult, CheckConnection());
			});
			this.DefaultPCResult = generalSetting.CurrentValue.DefaultPCResult;
			this.CEQuery = new CEQuery(JPCHost, OAUTHost, ClientId, ClientSecure) ?? throw new ArgumentNullException(nameof(this.CEQuery));
		}

		public QueryStatus CheckConnection()
		{
			CEAttres ceAttres = new CEAttres();
			ceAttres.AddAttribute(new CEAttribute("emteams", "getkeywords", CEAttributeType.XacmlString));
			CERequest ceReq = CreateQueryReq(TeamAction.Keywords_Query, string.Empty, "emteams1", ceAttres, "emteams2", "emteams3", new CEAttres());
			CEQuery.RefreshToken();
			QueryStatus emQueryRes = CEQuery.CheckResource(ceReq, out _, out _);
			return emQueryRes;
		}

		public CERequest CreateQueryReq(string strAction, string remoteAddress, string srcName,
				CEAttres ceSrcAttr, string userSid, string userName, CEAttres ceUserAttr)
		{
			if (!string.IsNullOrEmpty(strAction) && !string.IsNullOrEmpty(srcName))
			{
				CERequest obReq = new CERequest();

				// Action
				obReq.SetAction(strAction);

				// Host, remoteAddress should set as string.Empty, not null. Otherwise, will response 'E_ResponseStatusAbnormal' status.
				if (!string.IsNullOrEmpty(remoteAddress) && !remoteAddress.Contains(":")) //Not support IPV6
				{
					obReq.SetHost(remoteAddress, remoteAddress, null);
				}

				// Resource, Case Sensitive, Essential
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

		public QueryStatus QueryCloudAZPC(CERequest obReq, out List<CEObligation> listObligation, out PolicyResult emPolicyResult)
		{
			QueryStatus emQueryRes = QueryStatus.E_Failed;
			emPolicyResult = PolicyResult.DontCare;
			listObligation = new List<CEObligation>();
			if (obReq != null)
			{
				int retryCount = 0;
				while (retryCount++ < MAXRETRY)
				{
					emQueryRes = CEQuery.CheckResource(obReq, out emPolicyResult, out listObligation);

					if (emQueryRes == QueryStatus.E_Unauthorized)
					{
						CEQuery.RefreshToken();
						emQueryRes = CEQuery.CheckResource(obReq, out emPolicyResult, out listObligation);
					}

					if (emQueryRes == QueryStatus.S_OK) break;
				}
			}
			return emQueryRes;
		}

		public QueryStatus MultipleQueryColuAZPC(List<CERequest> ceRequests, out List<PolicyResult> listPolicyResults, out List<List<CEObligation>> listObligations)
		{
			QueryStatus emQueryRes = QueryStatus.E_Failed;
			listPolicyResults = new List<PolicyResult>();
			listObligations = new List<List<CEObligation>>();
			int retryCount = 0;
			while (retryCount++ < MAXRETRY)
			{
				emQueryRes = CEQuery.CheckMultipleResources(ceRequests, out listPolicyResults, out listObligations);

				if (emQueryRes == QueryStatus.E_Unauthorized)
				{
					CEQuery.RefreshToken();
					emQueryRes = CEQuery.CheckMultipleResources(ceRequests, out listPolicyResults, out listObligations);
				}

				if (emQueryRes == QueryStatus.S_OK) break;
			}

			return emQueryRes;
		}
	}
}
