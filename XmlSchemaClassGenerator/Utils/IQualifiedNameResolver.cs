using System.Xml;
using System.Xml.Schema;
using XmlSchemaClassGenerator.Model;

namespace XmlSchemaClassGenerator.Utils;

internal interface IQualifiedNameResolver
{
    XmlQualifiedName Resolve(
        TypeModel typeModel,
        XmlSchemaParticle xmlParticle,
        XmlSchemaElementEx element);
}