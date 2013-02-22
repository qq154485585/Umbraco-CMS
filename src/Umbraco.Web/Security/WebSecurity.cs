﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Security;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Web.UI.Pages;
using umbraco;
using umbraco.BusinessLogic;
using umbraco.DataLayer;

namespace Umbraco.Web.Security
{

    /// <summary>
    /// A utility class used for dealing with security in Umbraco
    /// </summary>
    public static class WebSecurity
    {

        /// <summary>
        /// Gets the SQL helper.
        /// </summary>
        /// <value>The SQL helper.</value>
        private static ISqlHelper SqlHelper
        {
            get { return Application.SqlHelper; }
        }

        private const long TicksPrMinute = 600000000;
        private static readonly int UmbracoTimeOutInMinutes = Core.Configuration.GlobalSettings.TimeOutInMinutes;

        /// <summary>
        /// Gets the current user.
        /// </summary>
        /// <value>The current user.</value>
        public static User CurrentUser
        {
            get
            {
                return User.GetCurrent();
            }
        }

        /// <summary>
        /// Logs a user in.
        /// </summary>
        /// <param name="u">The user</param>
        public static void PerformLogin(User u)
        {
            var retVal = Guid.NewGuid();
            SqlHelper.ExecuteNonQuery(
                                      "insert into umbracoUserLogins (contextID, userID, timeout) values (@contextId,'" + u.Id + "','" +
                                      (DateTime.Now.Ticks + (TicksPrMinute * UmbracoTimeOutInMinutes)).ToString() +
                                      "') ",
                                      SqlHelper.CreateParameter("@contextId", retVal));
            UmbracoUserContextId = retVal.ToString();

            LogHelper.Info(typeof(WebSecurity), "User {0} (Id: {1}) logged in", () => u.Name, () => u.Id);

        }

        /// <summary>
        /// Clears the current login for the currently logged in user
        /// </summary>
        public static void ClearCurrentLogin()
        {
            // Added try-catch in case login doesn't exist in the database
            // Either due to old cookie or running multiple sessions on localhost with different port number
            try
            {
                SqlHelper.ExecuteNonQuery(
                "DELETE FROM umbracoUserLogins WHERE contextId = @contextId",
                SqlHelper.CreateParameter("@contextId", UmbracoUserContextId));
            }
            catch (Exception ex)
            {
                LogHelper.Error(typeof(WebSecurity), string.Format("Login with contextId {0} didn't exist in the database", UmbracoUserContextId), ex);
            }
        }

        public static void RenewLoginTimeout()
        {
            // only call update if more than 1/10 of the timeout has passed
            SqlHelper.ExecuteNonQuery(
                "UPDATE umbracoUserLogins SET timeout = @timeout WHERE contextId = @contextId",
                SqlHelper.CreateParameter("@timeout", DateTime.Now.Ticks + (TicksPrMinute * UmbracoTimeOutInMinutes)),
                SqlHelper.CreateParameter("@contextId", UmbracoUserContextId));
        }

        internal static void UpdateLogin(long timeout)
        {
            // only call update if more than 1/10 of the timeout has passed
            if (timeout - (((TicksPrMinute * UmbracoTimeOutInMinutes) * 0.8)) < DateTime.Now.Ticks)
                SqlHelper.ExecuteNonQuery(
                    "UPDATE umbracoUserLogins SET timeout = @timeout WHERE contextId = @contextId",
                    SqlHelper.CreateParameter("@timeout", DateTime.Now.Ticks + (TicksPrMinute * UmbracoTimeOutInMinutes)),
                    SqlHelper.CreateParameter("@contextId", UmbracoUserContextId));
        }

        internal static long GetTimeout(string umbracoUserContextId)
        {
            //TODO: Clean this up! We should be using the CacheHelper (ApplicationContext.Current.ApplicationCache) 
            //  for all things cache related

            if (HttpRuntime.Cache["UmbracoUserContextTimeout" + umbracoUserContextId] == null)
            {
                HttpRuntime.Cache.Insert(
                    "UmbracoUserContextTimeout" + umbracoUserContextId,
                        GetTimeout(true),
                    null,
                    DateTime.Now.AddMinutes(UmbracoTimeOutInMinutes / 10), System.Web.Caching.Cache.NoSlidingExpiration);


            }

            var timeout = HttpRuntime.Cache["UmbracoUserContextTimeout" + umbracoUserContextId];
            if (timeout != null)
                return (long)timeout;

            return 0;

        }

        internal static long GetTimeout(bool byPassCache)
        {
            if (UmbracoSettings.KeepUserLoggedIn)
                RenewLoginTimeout();

            if (byPassCache)
            {
                return SqlHelper.ExecuteScalar<long>("select timeout from umbracoUserLogins where contextId=@contextId",
                                                          SqlHelper.CreateParameter("@contextId", new Guid(UmbracoUserContextId))
                                        );
            }
            
            return GetTimeout(UmbracoUserContextId);
        }

        /// <summary>
        /// Gets the user id.
        /// </summary>
        /// <param name="umbracoUserContextId">The umbraco user context ID.</param>
        /// <returns></returns>
        public static int GetUserId(string umbracoUserContextId)
        {
            try
            {
                //TODO: Clean this up! We should be using the CacheHelper (ApplicationContext.Current.ApplicationCache) 
                //  for all things cache related

                if (HttpRuntime.Cache["UmbracoUserContext" + umbracoUserContextId] == null)
                {
                    HttpRuntime.Cache.Insert(
                        "UmbracoUserContext" + umbracoUserContextId,
                        SqlHelper.ExecuteScalar<int>("select userID from umbracoUserLogins where contextID = @contextId",
                                                     SqlHelper.CreateParameter("@contextId", new Guid(umbracoUserContextId))
                            ),
                        null,
                        System.Web.Caching.Cache.NoAbsoluteExpiration,
                        new TimeSpan(0, (int)(UmbracoTimeOutInMinutes / 10), 0));
                }
                return (int)HttpRuntime.Cache["UmbracoUserContext" + umbracoUserContextId];
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Validates the user context ID.
        /// </summary>
        /// <param name="currentUmbracoUserContextId">The umbraco user context ID.</param>
        /// <returns></returns>
        public static bool ValidateUserContextId(string currentUmbracoUserContextId)
        {
            if ((currentUmbracoUserContextId != ""))
            {
                int uid = GetUserId(currentUmbracoUserContextId);
                long timeout = GetTimeout(currentUmbracoUserContextId);

                if (timeout > DateTime.Now.Ticks)
                {
                    return true;
                }
                var user = global::umbraco.BusinessLogic.User.GetUser(uid);
                LogHelper.Info(typeof(WebSecurity), "User {0} (Id:{1}) logged out", () => user.Name, () => user.Id);
            }
            return false;
        }

        //TODO: Clean this up!! We also have extension methods in StringExtensions for decrypting/encrypting in med trust
        // ... though an existing cookie may fail decryption, in that case they'd just get logged out. no problems.

        /// <summary>
        /// Gets or sets the umbraco user context ID.
        /// </summary>
        /// <value>The umbraco user context ID.</value>
        public static string UmbracoUserContextId
        {
            get
            {
                // zb-00004 #29956 : refactor cookies names & handling
                if (StateHelper.Cookies.HasCookies && StateHelper.Cookies.UserContext.HasValue)
                    return StateHelper.Cookies.UserContext.GetValue();
                try
                {
                    var encTicket = StateHelper.Cookies.UserContext.GetValue();
                    if (!string.IsNullOrEmpty(encTicket))
                        return FormsAuthentication.Decrypt(encTicket).UserData;
                }
                catch (HttpException ex)
                {
                    // we swallow this type of exception as it happens if a legacy (pre 4.8.1) cookie is set
                }
                catch (ArgumentException ex)
                {
                    // we swallow this one because it's 99.99% certaincy is legacy based. We'll still log it, though
                    LogHelper.Error(typeof(WebSecurity), "An error occurred reading auth cookie value", ex);
                }
                return "";
            }
            set
            {
                // zb-00004 #29956 : refactor cookies names & handling
                if (StateHelper.Cookies.HasCookies)
                {
                    // Clearing all old cookies before setting a new one.
                    if (StateHelper.Cookies.UserContext.HasValue)
                        StateHelper.Cookies.ClearAll();

                    if (!String.IsNullOrEmpty(value))
                    {
                        var ticket = new FormsAuthenticationTicket(1,
                        value,
                        DateTime.Now,
                        DateTime.Now.AddDays(1),
                        false,
                        value,
                        FormsAuthentication.FormsCookiePath);

                        // Encrypt the ticket.
                        var encTicket = FormsAuthentication.Encrypt(ticket);


                        // Create new cookie.
                        StateHelper.Cookies.UserContext.SetValue(value, 1);


                    }
                    else
                    {
                        StateHelper.Cookies.UserContext.Clear();
                    }
                }
            }
        }

    }
}