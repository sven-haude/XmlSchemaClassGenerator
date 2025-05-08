using System;

namespace XmlSchemaClassGenerator.Builder
{
    internal static class NamespaceResolver
    {
        public static string GetNamespaceName(GeneratorConfiguration configuration, Uri source, string xmlNamespace)
        {
            var key = new NamespaceKey(source, xmlNamespace);
            var result = configuration.NamespaceProvider.FindNamespace(key);
            if (string.IsNullOrEmpty(result))
                throw new ArgumentException($"Namespace {xmlNamespace} not provided through map or generator.");
            return result;
        }
    }
}