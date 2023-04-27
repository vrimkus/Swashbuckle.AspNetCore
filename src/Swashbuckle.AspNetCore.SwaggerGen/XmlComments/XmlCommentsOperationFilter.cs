using System;
using System.Reflection;
using System.Xml.XPath;
using Microsoft.OpenApi.Models;

namespace Swashbuckle.AspNetCore.SwaggerGen
{
    public class XmlCommentsOperationFilter : IOperationFilter
    {
        private readonly XPathNavigator _xmlNavigator;

        public XmlCommentsOperationFilter(XPathDocument xmlDoc)
        {
            _xmlNavigator = xmlDoc.CreateNavigator();
        }

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (context.MethodInfo == null) return;

            // If method is from a constructed generic type, look for comments from the generic type method
            var targetMethod = context.MethodInfo.DeclaringType.IsConstructedGenericType
                ? context.MethodInfo.GetUnderlyingGenericTypeMethod()
                : context.MethodInfo;

            if (targetMethod == null) return;

            ApplyControllerTags(operation, targetMethod.DeclaringType);
            ApplyMethodTags(operation, targetMethod);
        }

        private void ApplyControllerTags(OpenApiOperation operation, Type controllerType)
        {
            var typeMemberName = XmlCommentsNodeNameHelper.GetMemberNameForType(controllerType);
            var responseNodes = _xmlNavigator.Select($"/doc/members/member[@name='{typeMemberName}']/response");
            ApplyResponseTags(operation, responseNodes);
        }

        private void ApplyMethodTags(OpenApiOperation operation, MethodInfo methodInfo)
        {
            var methodMemberName = XmlCommentsNodeNameHelper.GetMemberNameForMethod(methodInfo);
            var methodNode = _xmlNavigator.SelectSingleNode($"/doc/members/member[@name='{methodMemberName}']");

            if (methodNode == null) return;

            /*
             * When including XML Comments using the `<include>` tag,
             * the generated XML Documentation file may contain a nested node, with a variable name.
             * We can leverage the XPath `descendant` axis to allow selection of the desired nodes,
             * irrespective of their nesting level.
             */
            var summaryNode = methodNode.SelectSingleNode("descendant::summary");
            if (summaryNode != null)
                operation.Summary = XmlCommentsTextHelper.Humanize(summaryNode.InnerXml);

            var remarksNode = methodNode.SelectSingleNode("descendant::remarks");
            if (remarksNode != null)
                operation.Description = XmlCommentsTextHelper.Humanize(remarksNode.InnerXml);

            var responseNodes = methodNode.Select("descendant::response");
            ApplyResponseTags(operation, responseNodes);
        }

        private void ApplyResponseTags(OpenApiOperation operation, XPathNodeIterator responseNodes)
        {
            while (responseNodes.MoveNext())
            {
                var code = responseNodes.Current.GetAttribute("code", "");
                var response = operation.Responses.ContainsKey(code)
                    ? operation.Responses[code]
                    : operation.Responses[code] = new OpenApiResponse();

                response.Description = XmlCommentsTextHelper.Humanize(responseNodes.Current.InnerXml);
            }
        }
    }
}