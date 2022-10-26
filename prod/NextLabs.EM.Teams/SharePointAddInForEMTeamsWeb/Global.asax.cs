using NextLabs.Data;
using NextLabs.GraphApp;
using QueryCloudAZSDK;
using QueryCloudAZSDK.CEModel;
using System;
using System.Linq;
using System.Web.Configuration;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace SharePointAddInForEMTeamsWeb
{
	public class MvcApplication : System.Web.HttpApplication
	{
		private log4net.ILog logger = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		protected void Application_Start()
		{
			logger.Info("Application Starting");

			string ContentKeywords = WebConfigurationManager.AppSettings.Get("NextLabs:ContentKeywords");
			logger.Info($"Application_Start ContentKeywords: {ContentKeywords}");
			if (!string.IsNullOrEmpty(ContentKeywords)) GlobalConfigs.SetKeywords(ContentKeywords.Split(';'));

			CloudAZQuery.Init();
			if (CloudAZQuery.CheckConnection() != QueryStatus.S_OK)
			{
				logger.Info("Application_Start - CloudAZQuery failed, service stopping...");
				return;
			}

			NxlGraphClient.Init();
			if (!NxlGraphClient.CheckConnection())
			{
				logger.Info("Application_Start - NxlGraphClient failed, service stopping...");
				return;
			}

			using (var ctx = new NxlDBContext()) 
			{
				try
				{
					var teamAttrs = ctx.TeamAttrs.FirstOrDefault();
				}
				catch (Exception e)
				{ 
					logger.Info("Application_Start - DataBase Connection Error, service stopping...");
					logger.Error(string.Format("Application_Start - DataBase Connection Error: {0}", e));
					return;
				}
			}

			AreaRegistration.RegisterAllAreas();
			FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
			RouteConfig.RegisterRoutes(RouteTable.Routes);
			BundleConfig.RegisterBundles(BundleTable.Bundles);

			logger.Info("Application Started Success!");
		}
	}
}
