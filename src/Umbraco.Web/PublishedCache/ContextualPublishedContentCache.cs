﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Umbraco.Core.Models;

namespace Umbraco.Web.PublishedCache
{
    /// <summary>
    /// Provides access to cached documents in a specified context.
    /// </summary>
    public class ContextualPublishedContentCache : ContextualPublishedCache
    {
        private readonly IPublishedContentCache _cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContextualPublishedContentCache"/> class with a published content cache and a context.
        /// </summary>
        /// <param name="cache">A published content cache.</param>
        /// <param name="umbracoContext">A context.</param>
        internal ContextualPublishedContentCache(IPublishedContentCache cache, UmbracoContext umbracoContext)
            : base(umbracoContext, cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// Gets the inner IPublishedContentCache.
        /// </summary>
        /// <remarks>For unit tests.</remarks>
        internal IPublishedContentCache InnerCache { get { return _cache; } }

        /// <summary>
        /// Gets content identified by a route.
        /// </summary>
        /// <param name="route">The route</param>
        /// <param name="hideTopLevelNode">A value forcing the HideTopLevelNode setting.</param>
        /// <returns>The content, or null.</returns>
        /// <remarks>A valid route is either a simple path eg <c>/foo/bar/nil</c> or a root node id and a path, eg <c>123/foo/bar/nil</c>.</remarks>
        public IPublishedContent GetByRoute(string route, bool? hideTopLevelNode = null)
        {
            return _cache.GetByRoute(UmbracoContext, route, hideTopLevelNode);
        }

        /// <summary>
        /// Gets the route for a content identified by its unique identifier.
        /// </summary>
        /// <param name="contentId">The content unique identifier.</param>
        /// <returns>The route.</returns>
        public string GetRouteById(int contentId)
        {
            return _cache.GetRouteById(UmbracoContext, contentId);
        }
    }
}