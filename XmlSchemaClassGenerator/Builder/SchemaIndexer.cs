using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;

namespace XmlSchemaClassGenerator.Builder;

internal static class SchemaIndexer
{
    public static void IndexSchemas(
        XmlSchemaSet set,
        Dictionary<XmlQualifiedName, HashSet<XmlSchemaAttributeGroup>> attributeGroups,
        Dictionary<XmlQualifiedName, HashSet<XmlSchemaGroup>> groups)
    {
        // Auf Schema-Abhängigkeiten achten
        var ordered = DependencyResolver.Resolve(set);

        // Attribut-Gruppen indexieren
        foreach (var schema in ordered)
        {
            var currentAttributeGroups = schema.AttributeGroups.Values
                .Cast<XmlSchemaAttributeGroup>()
                .DistinctBy(g => g.QualifiedName.ToString());
            foreach (var attrGroup in currentAttributeGroups)
            {
                if (!attributeGroups.TryGetValue(attrGroup.QualifiedName, out var list))
                {
                    list = new HashSet<XmlSchemaAttributeGroup>();
                    attributeGroups[attrGroup.QualifiedName] = list;
                }
                list.Add(attrGroup);
            }
        }

        // Element-Gruppen indexieren
        foreach (var schema in ordered)
        {
            var currentGroups = schema.Groups.Values
                .Cast<XmlSchemaGroup>()
                .DistinctBy(g => g.QualifiedName.ToString());
            foreach (var grp in currentGroups)
            {
                if (!groups.TryGetValue(grp.QualifiedName, out var list))
                {
                    list = new HashSet<XmlSchemaGroup>();
                    groups[grp.QualifiedName] = list;
                }
                list.Add(grp);
            }
        }
    }
}