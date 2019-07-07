using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.OData.Edm;
using OdataToEntity.Query;
using OdataToEntity.Query.Builder;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OdataToEntity.AspNetCore
{
    public static class OeAspHelper
    {
        private readonly static ConcurrentDictionary<int, OeModelBoundProvider> _cache = new ConcurrentDictionary<int, OeModelBoundProvider>();

        private static void Build(IEdmModel edmModel, OeModelBoundSettingsBuilder modelBoundSettingsBuilder, int pageSize, bool navigationNextLink)
        {
            if (edmModel.EntityContainer != null)
                foreach (IEdmEntitySet entitySet in edmModel.EntityContainer.EntitySets())
                {
                    IEdmEntityType entityType = entitySet.EntityType();
                    modelBoundSettingsBuilder.SetPageSize(pageSize, entityType);

                    foreach (IEdmNavigationProperty navigationProperty in entityType.NavigationProperties())
                        if (navigationProperty.Type.IsCollection())
                        {
                            if (navigationNextLink)
                                modelBoundSettingsBuilder.SetNavigationNextLink(navigationNextLink, navigationProperty);
                        }
                }

            foreach (IEdmModel refModel in edmModel.ReferencedModels)
                Build(refModel, modelBoundSettingsBuilder, pageSize, navigationNextLink);
        }
        public static OeModelBoundProvider CreateModelBoundProvider(this HttpContext httpContext)
        {
            var edmModel = (IEdmModel)httpContext.RequestServices.GetService(typeof(IEdmModel));
            return httpContext.CreateModelBoundProvider(edmModel);
        }
        public static OeModelBoundProvider CreateModelBoundProvider(this HttpContext httpContext, IEdmModel edmModel)
        {
            var requestHeaders = (HttpRequestHeaders)httpContext.Request.Headers;
            int maxPageSize = GetMaxPageSize(requestHeaders);
            if (maxPageSize <= 0)
                return null;

            if (!_cache.TryGetValue(maxPageSize, out OeModelBoundProvider modelBoundProvider))
            {
                modelBoundProvider = CreateModelBoundProvider(edmModel, maxPageSize, false);
                _cache.TryAdd(maxPageSize, modelBoundProvider);
            }
            return modelBoundProvider;
        }
        public static OeModelBoundProvider CreateModelBoundProvider(IEdmModel edmModel, int pageSize, bool navigationNextLink)
        {
            if (pageSize > 0 || navigationNextLink)
            {
                var modelBoundSettingsBuilder = new OeModelBoundSettingsBuilder();
                Build(edmModel, modelBoundSettingsBuilder, pageSize, navigationNextLink);
                return modelBoundSettingsBuilder.Build();
            }

            return null;
        }
        public static int GetMaxPageSize(HttpRequestHeaders requestHeaders)
        {
            ((IDictionary<String, StringValues>)requestHeaders).TryGetValue("Prefer", out StringValues preferHeader);
            var headers = OeRequestHeaders.Parse(requestHeaders.HeaderAccept, preferHeader);
            return headers.MaxPageSize;
        }
    }
}