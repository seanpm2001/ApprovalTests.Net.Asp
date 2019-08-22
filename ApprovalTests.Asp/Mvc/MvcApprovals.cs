﻿using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Web.Mvc;
using ApprovalTests.Asp.Mvc.Utilities;
using ApprovalTests.ExceptionalExceptions;
using ApprovalTests.Html;
using ApprovalTests.Scrubber;
using ApprovalUtilities.Asp.Mvc;
using ApprovalUtilities.Asp.Mvc.Bindings;

namespace ApprovalTests.Asp.Mvc
{
    public class MvcApprovals
    {
        public static string GetUrlPostContents(string url, NameValueCollection nameValueCollection)
        {
            try
            {
                using (var webClient = new WebClient())
                {
                    var str1 = url.Substring(0, url.LastIndexOf("/"));
                    webClient.Encoding = Encoding.UTF8;
                    var resp = webClient.UploadValues(url, "POST", nameValueCollection);
                    var str2 = Encoding.UTF8.GetString(resp);

                    if (!str2.Contains("<base")) str2 = str2.Replace("<head>", $"<head><base href=\"{str1}\">");
                    return str2;
                }
            }
            catch (Exception ex)
            {
                throw Exceptional.Create<Exception>(ex,
                    "The following error occured while connecting to:\n{0}\nError:\n{1}", url, ex.Message);
            }
        }

        public static void VerifyMvcViaPost<T>(Func<T, ActionResult> func,
            NameValueCollection nameValueCollectionPostData)
        {
            var type = func.Target.GetType();
            var clazz = type.Name.Replace("Controller", string.Empty);
            var action = func.Method.Name;

            VerifyMvcViaPost<T>(clazz, action, type, nameValueCollectionPostData);
        }

        public static void VerifyMvcViaPost<ControllerUnderTest, ActionParameter>(
            Expression<Func<ControllerUnderTest, Func<ActionParameter, ActionResult>>> actionName,
            NameValueCollection nameValueCollectionPostData)
            where ControllerUnderTest : IController
        {
            var className = ReflectionUtility.GetControllerName<ControllerUnderTest>();
            var action = ControllerUtilities.GetMethodName(actionName.Body);

            VerifyMvcViaPost<ActionParameter>(className, action, typeof(ControllerUnderTest),
                nameValueCollectionPostData);
        }

        private static void VerifyMvcViaPost<T>(string clazz, string action, Type type,
            NameValueCollection nameValueCollectionPostData)
        {
            VerifyUrlViaPost(GetURL(clazz, action, new NameValueCollection {{"assemblyPath", type.Assembly.Location}}),
                nameValueCollectionPostData);
        }

        public static void VerifyMvcViaPost<T>(Func<T, ActionResult> func, T value)
        {
            var pieces = CreateFromActionParameter(value);
            VerifyMvcViaPost(func, pieces);
        }

        private static NameValueCollection CreateFromActionParameter<T>(T value)
        {
            var pieces = new NameValueCollection();
            foreach (var property in value.GetType().GetProperties())
                pieces.Add(property.Name, "" + property.GetValue(value, null));
            return pieces;
        }

        public static void VerifyMvcViaPost<ControllerUnderTest, ActionParameter>(
            Expression<Func<ControllerUnderTest, Func<ActionParameter, ActionResult>>> actionName,
            ActionParameter actionParameter)
            where ControllerUnderTest : IController
        {
            VerifyMvcViaPost(actionName, CreateFromActionParameter(actionParameter));
        }

        public static void VerifyMvcPage(Func<ActionResult> func, Func<string, string> scrubber = null)
        {
            var clazz = func.Target.GetType().Name.Replace("Controller", string.Empty);
            var action = func.Method.Name;
            VerifyMvcUrl(clazz, action, scrubber: scrubber);
        }

        private static void VerifyMvcUrl(string clazz, string action, NameValueCollection nvc = null,
            Func<string, string> scrubber = null)
        {
            VerifyWithException(() =>
            {
                var url = GetURL(clazz, action, nvc);
                AspApprovals.VerifyUrl(url,
                    ScrubberUtils.Combine(HtmlScrubbers.ScrubMvc, MvcScrubbers.ScrubMvcVerificationToken,
                        scrubber ?? ScrubberUtils.NO_SCRUBBER));
            });
        }

        private static void VerifyWithException(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                var webEx = ex.InnerException as WebException;
                if (webEx != null && webEx.Status == WebExceptionStatus.ConnectFailure)
                    throw Exceptional.Create<Exception>(ex,
                        "Unable to connect to the hosted/remote server. Please check your connection. \r\n\r\n");
                if (webEx != null && webEx.Response != null &&
                    (webEx.Response as HttpWebResponse).StatusCode == HttpStatusCode.NotFound)
                {
                    var message = @"404 Error: Page not Found.

ApprovalTests.Asp needs a bootstrap to work. 
Please verify your Global.asax.cs file has the following code

protected void Application_Start()
{
    ...
    UnitTestBootStrap.RegisterWithDebugCondition();
}

See an Example Test at: https://github.com/approvals/Approvals.Net.Asp/blob/master/ApprovalTests.Asp.Tests/Mvc/MvcTest.cs

";
                    throw new Exception(message, ex);
                }

                throw;
            }
        }

        public static void VerifyUrlViaPost(string url, NameValueCollection nameValueCollection)
        {
            VerifyWithException(() =>
            {
                HtmlApprovals.VerifyHtml(GetUrlPostContents(url, nameValueCollection), HtmlScrubbers.ScrubMvc);
            });
        }

        private static string GetURL(string clazz, string action, NameValueCollection nvcQueryString)
        {
            var queryString = nvcQueryString == null
                ? string.Empty
                : string.Join("&",
                    Array.ConvertAll(nvcQueryString.AllKeys,
                        key => string.Format("{0}={1}", key, nvcQueryString[key])));
            return $"http://localhost:{PortFactory.MvcPort}/{clazz}/{action}?{queryString}";
        }


        public static void VerifyMvcPage<ControllerUnderTest>(
            Expression<Func<ControllerUnderTest, Func<ActionResult>>> actionName, Func<string, string> scrubber = null)
            where ControllerUnderTest : TestableControllerBase
        {
            VerifyMvcUrl(ReflectionUtility.GetControllerName<ControllerUnderTest>(),
                ControllerUtilities.GetMethodName(actionName.Body), GetFilePathasQueryString<ControllerUnderTest>(),
                scrubber);
        }

        private static string GetAssemblyPath<ControllerUnderTest>()
        {
            return Path.GetDirectoryName(typeof(ControllerUnderTest).Assembly.Location);
        }

        private static NameValueCollection GetFilePathasQueryString<ControllerUnderTest>()
            where ControllerUnderTest : IController
        {
            return new NameValueCollection {{"assemblyPath", typeof(ControllerUnderTest).Assembly.Location}};
        }

        public static void VerifyApprovalBootstrap()
        {
            VerifyWithException(() =>
            {
                AspApprovals.VerifyUrl($"http://localhost:{PortFactory.MvcPort}/ApprovalTests/Echo/Testing123");
            });
        }
    }
}