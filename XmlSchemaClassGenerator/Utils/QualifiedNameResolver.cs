using System;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using XmlSchemaClassGenerator.Builder;
using XmlSchemaClassGenerator.Model;

namespace XmlSchemaClassGenerator.Utils;

internal static class QualifiedNameResolver
{
    internal static bool NameExists(XmlSchemaSet schemaSet, XmlQualifiedName name)
    {
        var elements = schemaSet.GlobalElements.Names.Cast<XmlQualifiedName>();
        var types = schemaSet.GlobalTypes.Names.Cast<XmlQualifiedName>();
        return elements.Concat(types).Any(n => n.Namespace == name.Namespace && name.Name.Equals(n.Name, StringComparison.OrdinalIgnoreCase));
    }
    
    internal static XmlQualifiedName GetQualifiedName(ModelBuilder builder,
        GeneratorConfiguration configuration,  
        TypeModel typeModel, XmlSchemaParticle xmlParticle, XmlSchemaElementEx element)
    {
        var elementQualifiedName = element.ElementSchemaType.QualifiedName;
    
        if (elementQualifiedName.IsEmpty)
        {
            elementQualifiedName = element.RefName;
    
            if (elementQualifiedName.IsEmpty)
            {
                // inner type, have to generate a type name
                var typeModelName = xmlParticle is XmlSchemaGroupRef groupRef ? groupRef.RefName : typeModel.XmlSchemaName;
                var typeName = configuration.NamingProvider.TypeNameFromElement(typeModelName.Name, element.QualifiedName.Name, element);
                elementQualifiedName = new XmlQualifiedName(typeName, typeModel.XmlSchemaName.Namespace);
                // try to avoid name clashes
                if (NameExists(builder._set, elementQualifiedName))
                    elementQualifiedName = new[] { Constants.ItemName, Constants.PropertyName, Constants.ElementName }.Select(s => new XmlQualifiedName(elementQualifiedName.Name + s, elementQualifiedName.Namespace)).First(n => !NameExists(builder._set, n));
            }
        }
    
        return elementQualifiedName;
    }
    
    // public static XmlQualifiedName Resolve(
    //     GeneratorConfiguration config,
    //     Func<XmlQualifiedName, bool> nameExists,
    //     XmlQualifiedName defaultName,
    //     XmlQualifiedName refName,
    //     XmlQualifiedName parentSchemaNamespace,
    //     XmlSchemaParticle xmlParticle,
    //     XmlSchemaElementEx element)
    // {
    //     var qn = defaultName;
    //
    //     if (qn.IsEmpty)
    //         qn = refName;
    //
    //     if (qn.IsEmpty)
    //     {
    //         var baseName = xmlParticle is XmlSchemaGroupRef grp 
    //             ? grp.RefName.Name 
    //             : element.GetParentTypeName(); // hier ggf. typeModel.XmlSchemaName.Name
    //         var candidate = config.NamingProvider.TypeNameFromElement(
    //             baseName, element.QualifiedName.Name, element);
    //         qn = new XmlQualifiedName(candidate, parentSchemaNamespace);
    //
    //         if (nameExists(qn))
    //         {
    //             foreach (var suffix in new[] { "Item", "Property", "Element" })
    //             {
    //                 var trial = new XmlQualifiedName(qn.Name + suffix, qn.Namespace);
    //                 if (!nameExists(trial))
    //                 {
    //                     qn = trial;
    //                     break;
    //                 }
    //             }
    //         }
    //     }
    //
    //     return qn;
    // }
}