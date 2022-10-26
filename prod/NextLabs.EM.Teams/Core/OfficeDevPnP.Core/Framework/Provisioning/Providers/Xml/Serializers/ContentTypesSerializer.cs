﻿using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Framework.Provisioning.Providers.Xml.Resolvers;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Linq.Expressions;
using OfficeDevPnP.Core.Extensions;

namespace OfficeDevPnP.Core.Framework.Provisioning.Providers.Xml.Serializers
{
    /// <summary>
    /// Class to serialize/deserialize the Content Types
    /// </summary>
    [TemplateSchemaSerializer(SerializationSequence = 1000, DeserializationSequence = 1000,
        MinimalSupportedSchemaVersion = XMLPnPSchemaVersion.V201605,
        Scope = SerializerScope.ProvisioningTemplate)]
    internal class ContentTypesSerializer : PnPBaseSchemaSerializer<ContentType>
    {
        public override void Deserialize(object persistence, ProvisioningTemplate template)
        {
            var contentTypes = persistence.GetPublicInstancePropertyValue("ContentTypes");

            if (contentTypes != null)
            {
                var expressions = new Dictionary<Expression<Func<ContentType, Object>>, IResolver>();

                // Define custom resolver for FieldRef.ID because needs conversion from String to GUID
                expressions.Add(c => c.FieldRefs[0].Id, new FromStringToGuidValueResolver());
                //document template
                expressions.Add(c => c.DocumentTemplate, new ExpressionValueResolver((s, v) => v.GetPublicInstancePropertyValue("TargetName")));
                //document set template
                expressions.Add(c => c.DocumentSetTemplate, new PropertyObjectTypeResolver<ContentType>(ct => ct.DocumentSetTemplate));
                //document set template - allowed content types
                expressions.Add(c => c.DocumentSetTemplate.AllowedContentTypes, new ExpressionCollectionValueResolver<string>((s) => s.GetPublicInstancePropertyValue("ContentTypeID").ToString()));
                //document set template - shared fields
                expressions.Add(c => c.DocumentSetTemplate.SharedFields, new ExpressionCollectionValueResolver<Guid>((s) => Guid.Parse(s.GetPublicInstancePropertyValue("ID").ToString())));
                //document set template - welcome page fields
                expressions.Add(c => c.DocumentSetTemplate.WelcomePageFields, new ExpressionCollectionValueResolver<Guid>((s) => Guid.Parse(s.GetPublicInstancePropertyValue("ID").ToString())));

                template.ContentTypes.AddRange(
                    PnPObjectsMapper.MapObjects<ContentType>(contentTypes,
                            new CollectionFromSchemaToModelTypeResolver(typeof(ContentType)),
                            expressions,
                            recursive: true)
                            as IEnumerable<ContentType>);
            }
        }

        public override void Serialize(ProvisioningTemplate template, object persistence)
        {
            if (template.ContentTypes != null && template.ContentTypes.Count > 0)
            {
                var baseNamespace = PnPSerializationScope.Current?.BaseSchemaNamespace;
                var contentTypeTypeName = $"{baseNamespace}.ContentType, {PnPSerializationScope.Current?.BaseSchemaAssemblyName}";
                var contentTypeType = Type.GetType(contentTypeTypeName, true);
                var documentSetTemplateTypeName = $"{baseNamespace}.DocumentSetTemplate, {PnPSerializationScope.Current?.BaseSchemaAssemblyName}";
                var documentSetTemplateType = Type.GetType(documentSetTemplateTypeName, true);
                var documentTemplateTypeName = $"{baseNamespace}.ContentTypeDocumentTemplate, {PnPSerializationScope.Current?.BaseSchemaAssemblyName}";
                var documentTemplateType = Type.GetType(documentTemplateTypeName, true);

                var expressions = new Dictionary<string, IResolver>();

                //document set template
                expressions.Add($"{contentTypeType.FullName}.DocumentSetTemplate", new PropertyObjectTypeResolver(documentSetTemplateType, "DocumentSetTemplate"));
                //document set template - allowed content types
                expressions.Add($"{contentTypeType.Namespace}.DocumentSetTemplateAllowedContentType.ContentTypeID", new ExpressionValueResolver((s, v) => s));
                //document set template - shared fields and welcome page fields (this expression also used to resolve fieldref collection ids because of same type name)
                expressions.Add($"{contentTypeType.Namespace}.FieldRefBase.ID", new ExpressionValueResolver((s, v) => v != null ? v.ToString() : s?.ToString()));
                //document template
                expressions.Add($"{contentTypeType.FullName}.DocumentTemplate", new DocumentTemplateFromModelToSchemaTypeResolver(documentTemplateType));

                persistence.GetPublicInstanceProperty("ContentTypes")
                    .SetValue(
                        persistence,
                        PnPObjectsMapper.MapObjects(template.ContentTypes,
                            new CollectionFromModelToSchemaTypeResolver(contentTypeType), expressions, true));
            }
        }
    }
}
