using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Web.Configuration;
using JSNLog.Exceptions;
using JSNLog.Infrastructure;
using System.Text.RegularExpressions;
using System.Web;
using System.Reflection;
using JSNLog.ValueInfos;
using JSNLog;

namespace JSNLog.Infrastructure
{
    internal class ConfigProcessor
    {
        /// <summary>
        /// Processes a configuration (such as the contents of the jsnlog element in web.config).
        /// 
        /// The configuration is processed into JavaScript, which configures the jsnlog client side library.
        /// </summary>
        /// <param name="requestId">
        /// requestId to be passed to the JSNLog library when setting its options.
        /// Could be null (when user didn't provide a request id).
        /// In that case, this method creates a request id itself.
        /// </param>
        /// <param name="xe">
        /// XmlElement to be processed
        /// </param>
        /// <param name="sb">
        /// All JavaScript needs to be written to this string builder.
        /// </param>
        public void ProcessRoot(XmlElement xe, string requestId, StringBuilder sb)
        {
            string userIp = HttpContext.Current.Request.UserHostAddress;
            ProcessRootExec(xe, sb, VirtualPathUtility.ToAbsolute, userIp, requestId ?? RequestId.Get(), true);
        }

        // This version is not reliant on sitting in a web site, so can be unit tested.
        // generateClosure - if false, no function closure is generated around the generated JS code. Only set to false when unit testing.
        // Doing this assumes that jsnlog.js has loaded before the code generated by the method is executed.
        //
        // You want to set this to false during unit testing, because then you need direct access outside the closure of variables
        // that are private to the closure, specifically dummyappenders that store the log messages you receive, so you can unit test them.
        public void ProcessRootExec(XmlElement xe, StringBuilder sb, Func<string, string> virtualToAbsoluteFunc, string userIp, string requestId, bool generateClosure)
        {
            var jsnlogConfiguration = XmlHelpers.DeserialiseXml<JsnlogConfiguration>(xe);
            Dictionary<string, string> appenderNames = new Dictionary<string, string>();

            string loggerProductionLibraryVirtualPath = jsnlogConfiguration.productionLibraryPath;
            bool loggerEnabled = jsnlogConfiguration.enabled;

            string loggerProductionLibraryPath = null;
            if (!string.IsNullOrEmpty(loggerProductionLibraryVirtualPath))
            {
                // Every hard coded path must be resolved. See the declaration of DefaultDefaultAjaxUrl
                loggerProductionLibraryPath = Utils.AbsoluteUrl(loggerProductionLibraryVirtualPath, virtualToAbsoluteFunc);
            }

            JavaScriptHelpers.WriteJavaScriptBeginTag(sb);
            if (generateClosure) 
            {
                JavaScriptHelpers.WriteLine(string.Format("var {0} = function ({1}) {{", Constants.GlobalMethodCalledAfterJsnlogJsLoaded, Constants.JsLogObjectName), sb); 
            }

            // Generate setOptions for JSNLog object itself

            var jsonFields = new List<string>();
            JavaScriptHelpers.AddJsonField(jsonFields, Constants.JsLogObjectClientIpOption, userIp, new StringValue());
            JavaScriptHelpers.AddJsonField(jsonFields, Constants.JsLogObjectRequestIdOption, requestId, new StringValue());

            JavaScriptHelpers.GenerateSetOptions(Constants.JsLogObjectName, jsnlogConfiguration, 
                appenderNames, virtualToAbsoluteFunc, sb, jsonFields);

            //#########################
            //// Set default value for defaultAjaxUrl attribute
            //attributeValues[Constants.] =
            //    new Value(AbsoluteUrl(Constants.DefaultDefaultAjaxUrl, virtualToAbsoluteFunc), new StringValue());

            if (loggerEnabled)
            {
                // Process all loggers and appenders. First process the appenders, because the loggers can be 
                // dependent on the appenders, and will use appenderNames to translate configuration appender names
                // to JavaScript names.

                int sequence = 0;

                GenerateCreateJavaScript(jsnlogConfiguration.ajaxAppenders, sb, virtualToAbsoluteFunc, appenderNames, ref sequence);
                GenerateCreateJavaScript(jsnlogConfiguration.consoleAppenders, sb, virtualToAbsoluteFunc, appenderNames, ref sequence);
                GenerateCreateJavaScript(jsnlogConfiguration.loggers, sb, virtualToAbsoluteFunc, appenderNames, ref sequence);
            }

            // -------------

            if (generateClosure) 
            {
                // Generate code to execute the function, in case jsnlog.js has already been loaded.
                // Wrap in try catch, so if jsnlog.js hasn't been loaded, the resulting exception will be swallowed.
                JavaScriptHelpers.WriteLine(string.Format("}}; try {{ {0}({1}); }} catch(e) {{}};", Constants.GlobalMethodCalledAfterJsnlogJsLoaded, Constants.JsLogObjectName), sb); 
            }
            JavaScriptHelpers.WriteJavaScriptEndTag(sb);

            // Write the script tag that loads jsnlog.js after the code generated from the web.config.
            // When using jsnlog.js as an AMD module or in a bundle, jsnlog.js will be loaded after that code as well,
            // and creating a similar situation in the default out of the box loading option makes it more likely
            // you pick up bugs during testing.
            if (!string.IsNullOrWhiteSpace(loggerProductionLibraryPath))
            {
                JavaScriptHelpers.WriteScriptTag(loggerProductionLibraryPath, sb);
            }
        }

        /// <summary>
        /// Generates JavaScript code for all passed in elements.
        /// </summary>
        /// <param name="elements"></param>
        /// <param name="sb">
        /// The JavaScript code is added here.
        /// </param>
        /// <param name="virtualToAbsoluteFunc"></param>
        /// <param name="appenderNames"></param>
        /// <param name="sequence">
        /// When the method is called, this number is not used with any element.
        /// When the method returns, it ensures that a number is returned that is not used with any element.
        /// </param>
        private void GenerateCreateJavaScript(IEnumerable<ICanCreateElement> elements, StringBuilder sb, 
            Func<string, string> virtualToAbsoluteFunc, Dictionary<string, string> appenderNames, ref int sequence)
        {
            if (elements == null) { return;  }

            foreach(var element in elements)
            {
                element.CreateElement(sb, appenderNames, sequence, virtualToAbsoluteFunc);
                sequence++;
            }
        }
    }
}
