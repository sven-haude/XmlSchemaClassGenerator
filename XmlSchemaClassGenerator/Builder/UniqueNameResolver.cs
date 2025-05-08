using System.Collections.Generic;
using System.Linq;
using XmlSchemaClassGenerator.Model;

namespace XmlSchemaClassGenerator.Builder;


internal static class UniqueNameResolver
{
    public static void Apply(IEnumerable<NamespaceModel> namespaces)
    {
        // Alle Typen über alle Namespaces gruppiert nach Name
        foreach (var typeGroup in namespaces
                     .SelectMany(ns => ns.Types.Values)
                     .GroupBy(t => t.Name))
        {
            int suffix = 2;
            // Bei Mehrfachbelegung: alle Typen ab dem zweiten um "_2", "_3", … erweitern
            foreach (var typeModel in typeGroup.Skip(1))
            {
                typeModel.Name += $"_{suffix}";
                suffix++;
            }
        }
    }
}
