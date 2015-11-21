﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Text.RegularExpressions;
using JSNLog.Exceptions;
using JSNLog.Infrastructure;

namespace JSNLog.ValueInfos
{
    internal class UrlValue : IValueInfo
    {
        private static Regex regexUrl = new Regex(Constants.RegexUrl);

        Func<string, string> _virtualToAbsoluteFunc = null;
        public UrlValue(Func<string, string> virtualToAbsoluteFunc)
        {
            _virtualToAbsoluteFunc = virtualToAbsoluteFunc;
        }

        public string ToJavaScript(string text)
        {
            if (!regexUrl.IsMatch(text))
            {
                throw new InvalidAttributeException(text);
            }

            string resolvedUrl = Utils.AbsoluteUrl(text, _virtualToAbsoluteFunc);
            return HttpUtility.JavaScriptStringEncode(resolvedUrl, true);
        }
    }
}
