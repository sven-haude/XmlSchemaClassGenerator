using System.Collections.Generic;
using System.Linq;
using System.Xml.Schema;

namespace XmlSchemaClassGenerator.Builder;

// neu: DependencyResolver.cs
public class DependencyResolver {
    public static List<XmlSchema> Resolve(XmlSchemaSet set) {
        var result = new List<XmlSchema>();
        var seen = new HashSet<XmlSchema>();
        foreach (XmlSchema schema in set.Schemas()) {
            Recurse(schema, result, seen);
        }
        return result;
    }
    private static void Recurse(XmlSchema schema, List<XmlSchema> order, HashSet<XmlSchema> seen) {
        if (!seen.Add(schema)) return;
        foreach (var ext in schema.Includes.OfType<XmlSchemaExternal>())
            if (ext.Schema != null)
                Recurse(ext.Schema, order, seen);
        order.Add(schema);
    }
}