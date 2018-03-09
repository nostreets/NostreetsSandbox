﻿using System;
using System.Linq;
using System.Web.Mvc;
using Newtonsoft.Json;
using Nostreets_Services.Domain;
using Nostreets_Services.Domain.Base;
using Nostreets_Services.Interfaces.Services;
using NostreetsExtensions;
using NostreetsExtensions.Utilities;

namespace Nostreets_Sandbox.Controllers
{
    [RoutePrefix("home")]
    public class HomeController : Controller
    {
        public HomeController()
        {
            _userService = _userService.WindsorResolve(App_Start.WindsorConfig.GetContainer());
        }

        public IUserService _userService = null;

        [Route]
        public ActionResult Index()
        {
            User user = null;
            if (!SessionManager.HasAnySessions() || !SessionManager.Get<bool>(SessionState.IsLoggedOn))
            {
                string userIp = HttpContext.Request.UserHostAddress;
                user = _userService.Where(a => a.Settings.IPAddresses.Any(b => b == userIp)).FirstOrDefault();
            }
            else
                user = _userService.SessionUser;

            return View(user);
        }

        [Route("~/emailComfirmation")]
        public ActionResult EmailComfirmation(string token)
        {
            if (token != null)
            {
                Token userToken = JsonConvert.DeserializeObject<Token>(token);
                _userService.ValidateEmail(userToken);
            }
            return Redirect("/home");
        }
    }
}