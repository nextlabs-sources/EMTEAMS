﻿using System;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Framework.Provisioning.Providers.Xml;
using OfficeDevPnP.Core.Utilities;
using OfficeDevPnP.Core.Framework.Provisioning.Providers;
using System.Linq;
using System.Diagnostics;

namespace Microsoft.SharePoint.Client
{
    /// <summary>
    /// This class will be used to provide access to the right base template configuration
    /// </summary>
    public static class BaseTemplateManager
    {
        /// <summary>
        /// Gets the base template.
        /// </summary>
        /// <param name="web">the target web to get template</param>
        /// <returns>Returns a ProvisioningTemplate object</returns>
        public static ProvisioningTemplate GetBaseTemplate(this Web web)
        {
            web.Context.Load(web, p => p.WebTemplate, p => p.Configuration);
            web.Context.ExecuteQueryRetry();

            //if (web.IsFeatureActive(PUBLISHING_FEATURE_WEB) && web.WebTemplate == "STS" && web.Configuration == 0)
            //{
            //    return GetBaseTemplate(web, "STS0PUBLISHING", 0);
            //}
            //else
            //{
            return GetBaseTemplate(web, web.WebTemplate, web.Configuration);
            //}
        }

        /// <summary>
        /// Gets the provisioning template of provided webtemplate and configuration.
        /// </summary>
        /// <param name="web">the target web</param>
        /// <param name="webTemplate">the name of the webtemplate</param>
        /// <param name="configuration">configuration of template</param>
        /// <returns>Returns a ProvisioningTemplate object</returns>
        public static ProvisioningTemplate GetBaseTemplate(this Web web, string webTemplate, short configuration)
        {

            ProvisioningTemplate provisioningTemplate = null;

            try
            {
                string baseTemplate = $"OfficeDevPnP.Core.Framework.Provisioning.BaseTemplates.{GetSharePointVersion()}.{webTemplate}{configuration}Template.xml";
                using (Stream stream = typeof(BaseTemplateManager).Assembly.GetManifestResourceStream(baseTemplate))
                {
                    // Figure out the formatter to use
                    XDocument z = XDocument.Load(stream);
                    var result = z.Root.Attributes().Where(a => a.IsNamespaceDeclaration).
                            GroupBy(a => a.Name.Namespace == XNamespace.None ? String.Empty : a.Name.LocalName,
                                    a => XNamespace.Get(a.Value)).
                            ToDictionary(g => g.Key,
                                         g => g.First());
                    var pnpns = result["pnp"];

                    stream.Seek(0, SeekOrigin.Begin);
                    // Get the XML document from the stream
                    ITemplateFormatter formatter = XMLPnPSchemaFormatter.GetSpecificFormatter(pnpns.NamespaceName);

                    // And convert it into a ProvisioningTemplate

                    provisioningTemplate = formatter.ToProvisioningTemplate(stream);
                }
            }
            catch (Exception ex)
            {
                OfficeDevPnP.Core.Diagnostics.Log.Error(ex, "Provisioning", "Error occured while retrieving basetemplate");
            }

            return provisioningTemplate;
        }


        private static string GetSharePointVersion()
        {
            Assembly asm = Assembly.GetAssembly(typeof(Site));
            AssemblyName name = asm.GetName();

            try
            {
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(asm.Location);
                string version = fvi.FileVersion;

                if (Version.TryParse(version, out Version v))
                {
                    if (v.Major == 14)
                    {
                        return "_2010";
                    }
                    else if (v.Major == 15)
                    {
                        return "_2013";

                    }
                    else if (v.Major == 16)
                    {
                        if (v.Build < 6000)
                        {
                            //if(v.MinorRevision < 4690)
                            //{
                            //    // Pre May 2018 CU
                            //    CacheManager.Instance.SharepointVersions.TryAdd(urlUri, SPVersion.SP2016Legacy);
                            //    return SPVersion.SP2016Legacy;
                            //}

                            return "_2016";
                        }
                        else if (v.Build > 10300 && v.Build < 19000)
                        {
                            
                            return "_2019";
                        }
                        else
                        {
                            return "SPO";
                        }
                    }
                }
            }
            catch
            {
                // catch errors here...if it goes wrong we'll fall back to the default logic, 2019 will return as 2016 at that point.
            }
            return "SPO";
        }

    }
}
