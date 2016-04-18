using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using SecureAccess.Utilities;

namespace CPS_Navigator.Controllers {
    public class ControllerBase : Controller {

        /// <summary>
        /// Error Handler Override to intercept and log unhandled exceptions and to redirect to a friendly error page.
        /// </summary>
        /// <param name="filterContext"></param>
        protected override void OnException(ExceptionContext filterContext) {
            ModelStateDictionary model;
            string input = string.Empty;
            string method = string.Empty;
            string controller = string.Empty;
            string action = string.Empty;

            // get method info from context
            controller = filterContext.RouteData.Values["controller"] != null ? filterContext.RouteData.Values["controller"].ToString() : string.Empty;
            action = filterContext.RouteData.Values["action"] != null ? filterContext.RouteData.Values["action"].ToString() : string.Empty;
            method = string.Format("{0} -> {1}", controller, action);

            // get input string from context
            if (filterContext.Controller.ViewData != null && filterContext.Controller.ViewData.ModelState != null) {
                model = filterContext.Controller.ViewData.ModelState;
                foreach (KeyValuePair<string, ModelState> item in model) {
                    string attmeptedValue = null;
                    if (item.Value != null && item.Value.Value != null)
                        attmeptedValue = item.Value.Value.AttemptedValue;

                    input += string.Format("{0} = {1} ", item.Key, attmeptedValue);
                }
            }

            // log error
            Logging.LogException(
                Logging.LogType.General,
                string.Format("Unhandled Exception at [Method] {0} [Inputs] {1}", method, input).Truncate(1500),
                filterContext.Exception,
                "Exception",
                Logging.Severity.Error);

            // Output a nice error page
            filterContext.ExceptionHandled = true;

            if (filterContext.HttpContext.Request.IsAjaxRequest()) {
                this.View("AjaxError").ExecuteResult(this.ControllerContext);
            }
            else
                this.View("Error").ExecuteResult(this.ControllerContext);
        }

    }
}