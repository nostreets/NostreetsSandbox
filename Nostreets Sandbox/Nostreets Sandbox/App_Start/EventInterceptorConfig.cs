﻿using Nostreets_Services.Domain;
using Nostreets_Services.Interfaces.Services;
using NostreetsExtensions;
using NostreetsExtensions.Utilities;
using NostreetsInterceptor;
using NostreetsRouter.Models.Responses;
using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;
using System.Web;

namespace Nostreets_Sandbox.Services.Database
{
    public class EventInterceptorConfig
    {
        public EventInterceptorConfig(IUserService userInject)
        {
            _userSrv = userInject;

        }

        IUserService _userSrv = null;

        [Validator("UserLogIn")]
        void GetCurrentUser(HttpApplication app)
        {
            if (app.Request.GetCookie("loggedIn") == null || app.Request.GetCookie("loggedIn") == "false") { NotLoggedIn(app); }
            else
            {
                // string uid = CacheManager.GetItem<string>("user");
                // if (uid == null) { NotLoggedIn(app); }

                User user = CacheManager.GetItem<User>("user");
                if (user == null) { NotLoggedIn(app); }
            }
        }

        void NotLoggedIn(HttpApplication app)
        {
            app.Response.SetCookie(new HttpCookie("loggedIn", "false"));
            if (CacheManager.Contains("user"))
            {
                CacheManager.DeleteItem("user");
            }

            app.CreateResponse(HttpStatusCode.Unauthorized, new ErrorResponse("User is not logged in..."));
        }



        
    }
}