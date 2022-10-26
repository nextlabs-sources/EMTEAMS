﻿using System.Collections.Generic;
using System.Linq;
using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core.Entities;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using File = Microsoft.SharePoint.Client.File;
using OfficeDevPnP.Core.Diagnostics;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers.Extensions;
using System;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using OfficeDevPnP.Core.Utilities;
using Microsoft.SharePoint.Client.Taxonomy;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers.TokenDefinitions;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers.Utilities;

namespace OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers
{
    internal class ObjectFiles : ObjectHandlerBase
    {
        private readonly string[] WriteableReadOnlyFields = new string[] { "publishingpagelayout", "contenttypeid" };

        // See https://support.office.com/en-us/article/Turn-scripting-capabilities-on-or-off-1f2c515f-5d7e-448a-9fd7-835da935584f?ui=en-US&rs=en-US&ad=US
        public static readonly string[] BlockedExtensionsInNoScript = new string[] { ".asmx", ".ascx", ".aspx", ".htc", ".jar", ".master", ".swf", ".xap", ".xsf" };
        public static readonly string[] BlockedLibrariesInNoScript = new string[] { "_catalogs/theme", "style library", "_catalogs/lt", "_catalogs/wp" };

        public override string Name
        {
            get { return "Files"; }
        }

        public override string InternalName => "Files";

        public override TokenParser ProvisionObjects(Web web, ProvisioningTemplate template, TokenParser parser, ProvisioningTemplateApplyingInformation applyingInformation)
        {
            using (var scope = new PnPMonitoredScope(this.Name))
            {
                // Check if this is not a noscript site as we're not allowed to write to the web property bag is that one
                bool isNoScriptSite = web.IsNoScriptSite();
                web.EnsureProperties(w => w.ServerRelativeUrl, w => w.Url);

                // Build on the fly the list of additional files coming from the Directories
                var directoryFiles = new List<Model.File>();
                foreach (var directory in template.Directories)
                {
                    var metadataProperties = directory.GetMetadataProperties();
                    directoryFiles.AddRange(directory.GetDirectoryFiles(metadataProperties));
                }

                var filesToProcess = template.Files.Union(directoryFiles).ToArray();

                var siteAssetsFiles = filesToProcess.Where(f => f.Folder.ToLower().Contains("siteassets")).FirstOrDefault();
                if (siteAssetsFiles != null)
                {
                    // Need this so that we dont have access denied error during the first time upload, especially for modern sites
                    web.Lists.EnsureSiteAssetsLibrary();
                    web.Context.ExecuteQueryRetry();
                }

                var currentFileIndex = 0;
                var originalWeb = web; // Used to store and re-store context in case files are deployed to masterpage gallery
                foreach (var file in filesToProcess)
                {
                    file.Src = parser.ParseString(file.Src);
                    var targetFileName = parser.ParseString(
                        !String.IsNullOrEmpty(file.TargetFileName) ? file.TargetFileName : template.Connector.GetFilenamePart(file.Src)
                        );

                    currentFileIndex++;
                    WriteSubProgress("File", targetFileName, currentFileIndex, filesToProcess.Length);
                    var folderName = parser.ParseString(file.Folder);

                    if (folderName.ToLower().Contains("/_catalogs/"))
                    {
                        // Edge case where you have files in the template which should be provisioned to the site collection
                        // master page gallery and not to a connected subsite
                        web = web.Context.GetSiteCollectionContext().Web;
                        web.EnsureProperties(w => w.ServerRelativeUrl, w => w.Url);
                    }

                    if (folderName.ToLower().StartsWith((web.ServerRelativeUrl.ToLower())))
                    {
                        folderName = folderName.Substring(web.ServerRelativeUrl.Length);
                    }

                    if (SkipFile(isNoScriptSite, targetFileName, folderName))
                    {
                        // add log message
                        scope.LogWarning(CoreResources.Provisioning_ObjectHandlers_Files_SkipFileUpload, targetFileName, folderName);
                        continue;
                    }

                    var folder = web.EnsureFolderPath(folderName);

                    var checkedOut = false;

                    var targetFile = folder.GetFile(template.Connector.GetFilenamePart(targetFileName));

                    if (targetFile != null)
                    {
                        if (file.Overwrite)
                        {
                            scope.LogDebug(CoreResources.Provisioning_ObjectHandlers_Files_Uploading_and_overwriting_existing_file__0_, targetFileName);
                            checkedOut = CheckOutIfNeeded(web, targetFile);

                            using (var stream = FileUtilities.GetFileStream(template, file))
                            {
                                targetFile = UploadFile(folder, stream, targetFileName, file.Overwrite);
                            }
                        }
                        else
                        {
                            checkedOut = CheckOutIfNeeded(web, targetFile);
                        }
                    }
                    else
                    {
                        using (var stream = FileUtilities.GetFileStream(template, file))
                        {
                            if (stream == null)
                            {
                                throw new FileNotFoundException($"File {file.Src} does not exist");
                            }
                            else
                            {
                                scope.LogDebug(CoreResources.Provisioning_ObjectHandlers_Files_Uploading_file__0_, targetFileName);
                                targetFile = UploadFile(folder, stream, targetFileName, file.Overwrite);
                            }
                        }

                        checkedOut = CheckOutIfNeeded(web, targetFile);
                    }

                    if (targetFile != null)
                    {
                        // Add the fileuniqueid tokens
#if !SP2013
                        targetFile.EnsureProperties(p => p.UniqueId, p => p.ServerRelativeUrl);
                        parser.AddToken(new FileUniqueIdToken(web, targetFile.ServerRelativeUrl.Substring(web.ServerRelativeUrl.Length).TrimStart("/".ToCharArray()), targetFile.UniqueId));
                        parser.AddToken(new FileUniqueIdEncodedToken(web, targetFile.ServerRelativeUrl.Substring(web.ServerRelativeUrl.Length).TrimStart("/".ToCharArray()), targetFile.UniqueId));
#endif

#if !SP2013
                        bool webPartsNeedLocalization = false;
#endif
                        if (file.WebParts != null && file.WebParts.Any())
                        {
                            targetFile.EnsureProperties(f => f.ServerRelativeUrl);

                            var existingWebParts = web.GetWebParts(targetFile.ServerRelativeUrl).ToList();
                            foreach (var webPart in file.WebParts)
                            {
                                // check if the webpart is already set on the page
                                if (existingWebParts.FirstOrDefault(w => w.WebPart.Title == parser.ParseString(webPart.Title)) == null)
                                {
                                    scope.LogDebug(CoreResources.Provisioning_ObjectHandlers_Files_Adding_webpart___0___to_page, webPart.Title);
                                    var wpEntity = new WebPartEntity();
                                    wpEntity.WebPartTitle = parser.ParseString(webPart.Title);
                                    wpEntity.WebPartXml = parser.ParseXmlString(webPart.Contents).Trim(new[] { '\n', ' ' });
                                    wpEntity.WebPartZone = webPart.Zone;
                                    wpEntity.WebPartIndex = (int)webPart.Order;
                                    var wpd = web.AddWebPartToWebPartPage(targetFile.ServerRelativeUrl, wpEntity);
#if !SP2013
                                    if (webPart.Title.ContainsResourceToken())
                                    {
                                        // update data based on where it was added - needed in order to localize wp title
                                        wpd.EnsureProperties(w => w.ZoneId, w => w.WebPart, w => w.WebPart.Properties);
                                        webPart.Zone = wpd.ZoneId;
                                        webPart.Order = (uint)wpd.WebPart.ZoneIndex;
                                        webPartsNeedLocalization = true;
                                    }
#endif
                                }
                            }
                        }

#if !SP2013
                        if (webPartsNeedLocalization)
                        {
                            file.LocalizeWebParts(web, parser, targetFile, scope);
                        }
#endif
                        
                        //Set Properties before Checkin
                        if (file.Properties != null && file.Properties.Any())
                        {
                            Dictionary<string, string> transformedProperties = file.Properties.ToDictionary(property => property.Key, property => parser.ParseString(property.Value));
                            SetFileProperties(targetFile, transformedProperties, parser, false);
                        }

                        switch (file.Level)
                        {
                            case Model.FileLevel.Published:
                                {
                                    targetFile.PublishFileToLevel(Microsoft.SharePoint.Client.FileLevel.Published);
                                    break;
                                }
                            case Model.FileLevel.Draft:
                                {
                                    targetFile.PublishFileToLevel(Microsoft.SharePoint.Client.FileLevel.Draft);
                                    break;
                                }
                            default:
                                {
                                    if (checkedOut)
                                    {
                                        targetFile.CheckIn("", CheckinType.MajorCheckIn);
                                        web.Context.ExecuteQueryRetry();
                                    }
                                    break;
                                }
                        }

                        // Don't set security when nothing is defined. This otherwise breaks on files set outside of a list
                        if (file.Security != null &&
                            (file.Security.ClearSubscopes == true || file.Security.CopyRoleAssignments == true || file.Security.RoleAssignments.Count > 0))
                        {
                            targetFile.ListItemAllFields.SetSecurity(parser, file.Security);
                        }
                    }

                    web = originalWeb; // restore context in case files are provisioned to the master page gallery #1059
                }
            }
            WriteMessage("Done processing files", ProvisioningMessageType.Completed);
            return parser;
        }

        private static bool CheckOutIfNeeded(Web web, File targetFile)
        {
            var checkedOut = false;
            try
            {
                web.Context.Load(targetFile, f => f.CheckOutType, f => f.CheckedOutByUser, f => f.ListItemAllFields.ParentList.ForceCheckout);
                web.Context.ExecuteQueryRetry();

                if (targetFile.ListItemAllFields.ServerObjectIsNull.HasValue
                    && !targetFile.ListItemAllFields.ServerObjectIsNull.Value
                    && targetFile.ListItemAllFields.ParentList.ForceCheckout)
                {
                    if (targetFile.CheckOutType == CheckOutType.None)
                    {
                        targetFile.CheckOut();
                    }
                    checkedOut = true;
                }
            }
            catch (ServerException ex)
            {
                // Handling the exception stating the "The object specified does not belong to a list."
                if (ex.ServerErrorCode != -2146232832)
                {
                    throw;
                }
            }
            return checkedOut;
        }


        public override ProvisioningTemplate ExtractObjects(Web web, ProvisioningTemplate template, ProvisioningTemplateCreationInformation creationInfo)
        {

            return template;
        }

        public void SetFileProperties(File file, IDictionary<string, string> properties, bool checkoutIfRequired = true)
        {
            SetFileProperties(file, properties, null, checkoutIfRequired);
        }

        public void SetFileProperties(File file, IDictionary<string, string> properties, TokenParser parser, bool checkoutIfRequired = true)
        {
            var context = file.Context;
            if (properties != null && properties.Count > 0)
            {
                // Get a reference to the target list, if any
                // and load file item properties
                var parentList = file.ListItemAllFields.ParentList;
                context.Load(parentList);
                context.Load(file.ListItemAllFields);
                try
                {
                    context.ExecuteQueryRetry();
                }
                catch (ServerException ex)
                {
                    // If this throws ServerException (does not belong to list), then shouldn't be trying to set properties)
                    if (ex.ServerErrorCode != -2146232832)
                    {
                        throw;
                    }
                }

                ListItemUtilities.UpdateListItem(file.ListItemAllFields, parser, properties, ListItemUtilities.ListItemUpdateType.UpdateOverwriteVersion);
            }
        }

        /// <summary>
        /// Checks if a given file can be uploaded. Sites using NoScript can't handle all uploads
        /// </summary>
        /// <param name="isNoScriptSite">Is this a noscript site?</param>
        /// <param name="fileName">Filename to verify</param>
        /// <param name="folderName">Folder (library) to verify</param>
        /// <returns>True is the file will not be uploaded, false otherwise</returns>
        public static bool SkipFile(bool isNoScriptSite, string fileName, string folderName)
        {
            string fileExtionsion = Path.GetExtension(fileName).ToLower();
            if (isNoScriptSite)
            {
                if (!String.IsNullOrEmpty(fileExtionsion) && BlockedExtensionsInNoScript.Contains(fileExtionsion))
                {
                    // We need to skip this file
                    return true;
                }

                if (!String.IsNullOrEmpty(folderName))
                {
                    foreach (string blockedlibrary in BlockedLibrariesInNoScript)
                    {
                        if (folderName.ToLower().StartsWith(blockedlibrary))
                        {
                            // Can't write to this library, let's skip
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public override bool WillProvision(Web web, ProvisioningTemplate template, ProvisioningTemplateApplyingInformation applyingInformation)
        {
            if (!_willProvision.HasValue)
            {
                _willProvision = template.Files.Any() | template.Directories.Any();
            }
            return _willProvision.Value;
        }

        public override bool WillExtract(Web web, ProvisioningTemplate template, ProvisioningTemplateCreationInformation creationInfo)
        {
            if (!_willExtract.HasValue)
            {
                _willExtract = false;
            }
            return _willExtract.Value;
        }

        private static File UploadFile(Microsoft.SharePoint.Client.Folder folder, Stream stream, string fileName, bool overwrite)
        {
            if (folder == null) throw new ArgumentNullException(nameof(folder));
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            File targetFile = null;
            try
            {
                targetFile = folder.UploadFile(fileName, stream, overwrite);
            }
            catch (ServerException ex)
            {
                if (ex.ServerErrorCode != -2130575306) //Error code: -2130575306 = The file is already checked out.
                {
                    //The file name might contain encoded characters that prevent upload. Decode it and try again.
                    fileName = WebUtility.UrlDecode(fileName);
                    try
                    {
                        targetFile = folder.UploadFile(fileName, stream, overwrite);
                    }
                    catch (Exception)
                    {
                        //unable to Upload file, just ignore
                    }
                }
            }
            return targetFile;
        }
    }
}