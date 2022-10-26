// Copyright (c) NextLabs Corporation. All rights reserved.


namespace NextLabs.SharePoint
{
	using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.Online.SharePoint.TenantAdministration;
	using Microsoft.SharePoint.Client;
	using NextLabs.Common;
	using OfficeDevPnP.Core;
	using OfficeDevPnP.Core.ALM;
    using OfficeDevPnP.Core.Enums;
    using System;
    using System.Collections.Generic;
    using System.Security;
	using System.Threading.Tasks;

	public class NxlSharePointClient
	{
		//List Add is not thread safe
		private readonly object objInstallLock = new object();
		private static List<string> listInstallingOrUpgrading = new List<string>();
		private readonly object objUninstallLock = new object();
		private static List<string> listUninstalling = new List<string>();

		private object changeLock = new object();
		private IOptionsMonitor<SharePointOptions> sharepointOptions;

		private ILogger logger;
		private string tenantURL;
		private string adminUserName;
		private SecureString adminPassword;
		private Guid appCatalogId;
		private string appTitle;

		public NxlSharePointClient(IOptionsMonitor<SharePointOptions> sharepointOptions, ILogger<NxlSharePointClient> logger)
		{
			this.logger = logger ?? throw new ArgumentNullException(nameof(this.logger));
			this.sharepointOptions = sharepointOptions ?? throw new ArgumentNullException(nameof(this.sharepointOptions));
			this.sharepointOptions.OnChange(sharepointOptions => {
				lock (changeLock) 
				{
					this.tenantURL = sharepointOptions.TenantURL ?? throw new ArgumentNullException(nameof(tenantURL));
					this.adminUserName = sharepointOptions.AdminUserName ?? throw new ArgumentNullException(nameof(adminUserName));
					this.adminPassword = ConvertToSecureString(sharepointOptions.AdminPassword) ?? throw new ArgumentNullException(nameof(adminPassword));
					string tempCatalogId = sharepointOptions.AppCatalogId ?? throw new ArgumentNullException(nameof(appCatalogId));
					this.appCatalogId = new Guid(tempCatalogId);
					this.appTitle = sharepointOptions.AppTitle ?? throw new ArgumentNullException(nameof(appTitle));
				}
				this.logger.LogInformation("SharePointOptions of NxlSharePointClient Changed, Connection: {bChecked}", Check());
			});
			this.tenantURL = sharepointOptions.CurrentValue.TenantURL ?? throw new ArgumentNullException(nameof(tenantURL));
			this.adminUserName = sharepointOptions.CurrentValue.AdminUserName ?? throw new ArgumentNullException(nameof(adminUserName));
			this.adminPassword = ConvertToSecureString(sharepointOptions.CurrentValue.AdminPassword) ?? throw new ArgumentNullException(nameof(adminPassword));
			string tempCatalogId = sharepointOptions.CurrentValue.AppCatalogId ?? throw new ArgumentNullException(nameof(appCatalogId));
			this.appCatalogId = new Guid(tempCatalogId);
			this.appTitle = sharepointOptions.CurrentValue.AppTitle ?? throw new ArgumentNullException(nameof(appTitle));
		}

		public bool Check()
		{
			bool tenantExisted = DoesSiteExist(tenantURL);
			bool appInCatalog = false;
			try
			{
				using ClientContext context = new ClientContext(tenantURL)
				{
					AuthenticationMode = ClientAuthenticationMode.Default,
					Credentials = new SharePointOnlineCredentials(adminUserName, adminPassword)
				};
				AppManager manager = new AppManager(context);
				AppMetadata c_app = manager.GetAvailable(appCatalogId); //if app isn't existed, will throw exception
				if(c_app.NotNull()) appInCatalog = true;
			}
			catch (Exception e) 
			{ 
				logger.LogError("Check() Error: It usually occurs due to SharePoint fields of appsettings.json misconfiguration. Details: {e}", e);
			}
			return tenantExisted && appInCatalog;
		}

		public bool DoesSiteExist(string siteUrl)
		{
			AuthenticationManager authMgr = new AuthenticationManager();
			try
			{
				// Get the client context  
				using var ctx = authMgr.GetSharePointOnlineAuthenticatedContextTenant(siteUrl, adminUserName, adminPassword);
				// Check if a web site exists at the specified full URL  
				return ctx.WebExistsFullUrl(siteUrl);
			}
			catch (Exception e)
			{
				logger.LogError("DoesSiteExist({siteUrl}) Error: {e}", siteUrl, e);
				return false;
			}
		}

		public void AutoInstallApp(string siteUrl, bool needUpgrade = false)
		{
			logger.LogDebug("AutoInstallApp({siteUrl}, {needUpgrade}) Start.", siteUrl, needUpgrade);
			bool actived = false, deActived = false;
			actived = AddSiteCollectionAdministrator(siteUrl);
			if (actived) InstallApp(siteUrl, needUpgrade);
			while (actived && !deActived) deActived = RemoveSiteCollectionAdministrator(siteUrl);
			logger.LogDebug("AutoInstallApp({siteUrl}, {needUpgrade}) End.", siteUrl, needUpgrade);
		}

		public void AutoUninstallApp(string siteUrl)
		{
			logger.LogDebug("AutoUninstallApp({siteUrl}) Start.", siteUrl);
			bool actived = false, deActived = false;
			actived = AddSiteCollectionAdministrator(siteUrl);
			if (actived) RemoveAppByTitle(siteUrl);
			while (actived && !deActived) deActived = RemoveSiteCollectionAdministrator(siteUrl);
			logger.LogDebug("AutoUninstallApp({siteUrl}) End.", siteUrl);
		}

		public void InstallApp(string siteUrl, bool needUpgrade = false, AppCatalogScope scope = AppCatalogScope.Tenant)
		{
			if (listInstallingOrUpgrading.Contains(siteUrl))
			{
				logger.LogError("InstallApp({siteUrl}, {needUpgrade}, {scope}) - app-{appTitle}:{appCatalogId} is being {done}.",
					siteUrl, needUpgrade, scope, appTitle, appCatalogId, (needUpgrade ? "installed/upgraded" : "installed"));
				return;
			}
			lock (objInstallLock) { listInstallingOrUpgrading.Add(siteUrl); }
			try
			{
				using ClientContext context = new ClientContext(siteUrl)
				{
					AuthenticationMode = ClientAuthenticationMode.Default,
					Credentials = new SharePointOnlineCredentials(adminUserName, adminPassword)
				};
				AppManager manager = new AppManager(context);
				AppMetadata c_app = manager.GetAvailable(appCatalogId);	//if app isn't existed, will throw exception
				logger.LogInformation("InstallApp({siteUrl}, {needUpgrade}, {scope}) - The app has found in tenant appCatalog, start to {do}.",
					siteUrl, needUpgrade, scope, (needUpgrade ? "install/update" : "install"));
				try
				{
					Task appTask = Task.Run(async () => await manager.InstallAsync(c_app));
					logger.LogInformation("InstallApp({siteUrl}, {needUpgrade}, {scope}) - Installing App at {siteUrl}.", siteUrl, needUpgrade, scope, siteUrl);
					appTask.Wait();
				}
				catch (Exception e)
				{
					if (e.InnerException.Message.Contains("An instance of this App already exists at the specified location", StringComparison.OrdinalIgnoreCase))
					{
						if (needUpgrade)
						{
							try
							{
								Task appTask = Task.Run(async () => await manager.UpgradeAsync(c_app));
								logger.LogInformation("InstallApp({siteUrl}, {needUpgrade}, {scope}) - Upgrading App at {siteUrl}.", siteUrl, needUpgrade, scope, siteUrl);
								appTask.Wait();
							}
							catch(Exception ex) 
							{ 
								logger.LogError("InstallApp({siteUrl}, {needUpgrade}, {scope}) Error: {ex}", siteUrl, needUpgrade, scope, ex);
							}
						}
						else
						{
							logger.LogInformation("InstallApp({siteUrl}, {needUpgrade}, {scope}) - An instance of this App already exists at {siteUrl}.", siteUrl, needUpgrade, scope, siteUrl);
						}
					}
					else
					{
						logger.LogError("InstallApp({siteUrl}, {needUpgrade}, {scope}) Error: {e}", siteUrl, needUpgrade, scope, e);
					}
				}
			}
			catch (Exception e)
			{
				logger.LogError("InstallApp({siteUrl}, {needUpgrade}, {scope}) Error: {e}", siteUrl, needUpgrade, scope, e);
			}
			if (listInstallingOrUpgrading.Contains(siteUrl)) listInstallingOrUpgrading.Remove(siteUrl);
		}

		public bool RemoveAppByTitle(string siteUrl)
		{
			// Create context for SharePoint online
			try
			{
				using ClientContext ctx = new ClientContext(siteUrl)
				{
					AuthenticationMode = ClientAuthenticationMode.Default,
					Credentials = new SharePointOnlineCredentials(adminUserName, adminPassword)
				};

				// Get variables for the operations
				Site site = ctx.Site;
				Web web = ctx.Web;

				bool actived = false;
				do
				{
					try
					{
						if (!actived)
						{
							// Make sure we have side loading enabled. 
							site.ActivateFeature(OfficeDevPnP.Core.Constants.FeatureId_Site_AppSideLoading);
							actived = true;
						}

						// Check and Uninstall the app
						web.RemoveAppInstanceByTitle(appTitle);
						break;
					}
					catch (Exception e)
					{
						logger.LogError("RemoveAppByTitle Error: {e}", e);
					}
				} while (true);

				if (actived) DeactiveSideLoading(ref site);
				return true;
			}
			catch (Exception e)
			{
				logger.LogError("RemoveAppByTitle Error: {e}", e);
				return false;
			}
		}

		private void DeactiveSideLoading(ref Site site)
		{
			bool deactived = false;
			do
			{
				try
				{
					// Disable side loading feature
					site.DeactivateFeature(OfficeDevPnP.Core.Constants.FeatureId_Site_AppSideLoading);
					deactived = true;
				}
				catch (Exception e)
				{
					logger.LogError("DeactiveSideLoading Error: {e}", e);
				}
			}
			while (!deactived);
		}

        #region SharePoint Adminstrator
        public bool AddSiteCollectionAdministrator(string siteUrl)
		{
			using ClientContext clientContext = new ClientContext(tenantURL)
			{
				AuthenticationMode = ClientAuthenticationMode.Default,
				Credentials = new SharePointOnlineCredentials(adminUserName, adminPassword)
			};

			var tenant = new Tenant(clientContext);

			User retUser = tenant.SetSiteAdmin(siteUrl, adminUserName, true);
			clientContext.Load(retUser, u => u.IsSiteAdmin);
			clientContext.ExecuteQuery();

			return retUser.IsSiteAdmin;
		}

		public bool RemoveSiteCollectionAdministrator(string siteUrl)
		{
			using ClientContext clientContext = new ClientContext(tenantURL)
			{
				AuthenticationMode = ClientAuthenticationMode.Default,
				Credentials = new SharePointOnlineCredentials(adminUserName, adminPassword)
			};

			var tenant = new Tenant(clientContext);

			User retUser = tenant.SetSiteAdmin(siteUrl, adminUserName, false);
			clientContext.Load(retUser, u => u.IsSiteAdmin);
			clientContext.ExecuteQuery();

			return !retUser.IsSiteAdmin;
		}
		#endregion

		#region Util
		private static SecureString ConvertToSecureString(string password)
		{
			if (password == null)
				throw new ArgumentNullException("password");

			var securePassword = new SecureString();

			foreach (char c in password)
				securePassword.AppendChar(c);

			securePassword.MakeReadOnly();
			return securePassword;
		}
		#endregion

		#region Useless now
		//It remove app from tenant appCatalog, NOT site
		public void UninstallApp(AppCatalogScope scope = AppCatalogScope.Tenant)
		{
			if (listUninstalling.Contains(tenantURL))
			{
				logger.LogError("UninstallApp - app of {tenantURL} is being uninstalled.", tenantURL);
				return;
			}
			lock (objUninstallLock) { listUninstalling.Add(tenantURL); }
			try
			{
				using ClientContext context = new ClientContext(tenantURL)
				{
					AuthenticationMode = ClientAuthenticationMode.Default,
					Credentials = new SharePointOnlineCredentials(adminUserName, adminPassword)
				};
				AppManager manager = new AppManager(context);
				logger.LogInformation("UninstallApp - Start to uninstall.");
				try
				{
					Task installTask = Task.Run(async () => await manager.RemoveAsync(appCatalogId, scope));
					logger.LogInformation("UninstallApp - Removing App from tenant appcatalog.");
					installTask.Wait();
				}
				catch (Exception e)
				{
					logger.LogError("UninstallApp Error: {e}", e);
				}
			}
			catch (Exception e)
			{
				logger.LogError("UninstallApp Error: {e}", e);
			}
			if (listUninstalling.Contains(tenantURL)) listUninstalling.Remove(tenantURL);
		}
		#endregion
	}
}
