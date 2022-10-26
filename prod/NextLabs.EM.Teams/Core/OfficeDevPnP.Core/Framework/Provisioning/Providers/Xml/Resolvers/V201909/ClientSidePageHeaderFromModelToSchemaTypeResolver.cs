﻿using OfficeDevPnP.Core.Framework.Provisioning.Model;
using System;
using System.Collections.Generic;

namespace OfficeDevPnP.Core.Framework.Provisioning.Providers.Xml.Resolvers.V201909
{
    internal class ClientSidePageHeaderFromModelToSchemaTypeResolver : ITypeResolver
    {
        public string Name => this.GetType().Name;

        public bool CustomCollectionResolver => false;


        public ClientSidePageHeaderFromModelToSchemaTypeResolver()
        {
        }

        public object Resolve(object source, Dictionary<String, IResolver> resolvers = null, Boolean recursive = false)
        {
            Object result = null;

            // Try with the tenant-wide AppCatalog
            BaseClientSidePage page;
            if (source is ClientSidePage)
            {
                page = source as ClientSidePage;
            }
            else
            {
                page = source as TranslatedClientSidePage;
            }
            
            //var page = source as Model.ClientSidePage;
            var header = page?.Header;

            if (null != header)
            {
                var headerTypeName = $"{PnPSerializationScope.Current?.BaseSchemaNamespace}.BaseClientSidePageHeader, {PnPSerializationScope.Current?.BaseSchemaAssemblyName}";
                var headerType = Type.GetType(headerTypeName, true);
                result = Activator.CreateInstance(headerType);

                PnPObjectsMapper.MapProperties(header, result, resolvers, recursive);
            }

            return (result);
        }
    }
}
