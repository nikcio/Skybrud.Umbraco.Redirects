﻿using System;
using System.Web;
using Skybrud.Umbraco.Redirects.Domains;
using Skybrud.Umbraco.Redirects.Events;
using Skybrud.Umbraco.Redirects.Models;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Routing;

namespace Skybrud.Umbraco.Redirects.Routing {
    
    /// <summary>
    /// HTTP module for handling inbound redirects.
    /// </summary>
    public class RedirectsInjectedModule : IHttpModule {

        private readonly IUmbracoContextAccessor _umbracoContextAccessor;

        private readonly IDomainService _domains;

        private readonly IRedirectsService _redirects;

        #region Constructors

        public RedirectsInjectedModule(IUmbracoContextAccessor umbracoContextAccessor, IDomainService domains, IRedirectsService redirects) {
            _umbracoContextAccessor = umbracoContextAccessor;
            _domains = domains;
            _redirects = redirects;
        }

        #endregion

        #region Public member methods

        /// <summary>
        /// Initializes the HTTP module.
        /// </summary>
        /// <param name="context"></param>
        public void Init(HttpApplication context) {
            context.EndRequest += ContextOnEndRequest;
        }
        
        /// <summary>
        /// Disposes the HTTP module.
        /// </summary>
        public void Dispose() { }

        #endregion

        #region Private member methods

        private void ContextOnEndRequest(object sender, EventArgs eventArgs) {

            HttpApplication application = (HttpApplication) sender;

            // Get a reference to the current HTTP context
            HttpContextBase context = new HttpContextWrapper(application.Context);

            // Ignore if not a 404 response
            if (application.Response.StatusCode != 404) return;

            // Get the Umbraco domain of the current request
            Domain domain = GetUmbracoDomain(application.Request);

            // Get the root node/content ID of the domain (no domain = 0)
            int rootNodeId = domain?.ContentId ?? 0;

            // Invoke the pre lookup event
            RedirectPreLookupEventArgs preLookupEventArgs = new RedirectPreLookupHttpContextEventArgs(context);
            _redirects.OnPreLookup(preLookupEventArgs);
            
            // Declare a variable for the redirect (pre lookup event may alreadu have set one)
            RedirectItem redirect = preLookupEventArgs.Redirect;

            // Look for a redirect matching the URL (and domain)
            if (redirect == null) {
                
                // Look for redirects via the redirects service
                if (rootNodeId > 0) redirect = _redirects.GetRedirectByUrl(rootNodeId, context.Request.RawUrl);
                redirect = redirect ?? _redirects.GetRedirectByUrl(0, context.Request.RawUrl);

                // Invoke the post lookup event
                RedirectPostLookupEventArgs postLookUpEventArgs = new RedirectPostLookupHttpContextEventArgs(context, redirect);
                _redirects.OnPostLookup(postLookUpEventArgs);

                // Update the local variable with the redirect from the event
                redirect = postLookUpEventArgs.Redirect;

            }

            // Return if we don't have a redirect at this point
            if (redirect == null) return;

			string redirectUrl = redirect.LinkUrl;

			if (redirect.ForwardQueryString) redirectUrl = _redirects.HandleForwardQueryString(redirect, context.Request.RawUrl);

            //if (redirect.IsRegex)
            //{
            //    var regex = new Regex(redirect.Url);

            //    if (_capturingGroupsRegex.IsMatch(redirectUrl))
            //    {
            //        redirectUrl = regex.Replace(redirect.Url, redirectUrl);
            //    }
            //}


            // Redirect to the URL
            if (redirect.IsPermanent) {
                application.Response.RedirectPermanent(redirectUrl);
            } else {
                application.Response.Redirect(redirectUrl);
            }

        }

        private Domain GetUmbracoDomain(HttpRequest request) {

            // Get the Umbraco request (it may be NULL)
            PublishedRequest pcr = _umbracoContextAccessor.UmbracoContext?.PublishedRequest;

            // Return the domain of the Umbraco request
            if (pcr != null) return pcr.Domain;

            // Find domain via DomainService based on current request domain
            var domain = DomainUtils.FindDomainForUri(_domains, request.Url);

            return domain;

        }

        #endregion
    }

}