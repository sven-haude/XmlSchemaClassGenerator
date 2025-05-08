using System.Xml;
using System.Xml.Schema;
using XmlSchemaClassGenerator.Builder;
using XmlSchemaClassGenerator.Model;

namespace XmlSchemaClassGenerator.Utils;

internal class DefaultQualifiedNameResolver : IQualifiedNameResolver
{
    private readonly ModelBuilder _builder;
    private readonly GeneratorConfiguration _configuration;

    public DefaultQualifiedNameResolver(ModelBuilder builder, GeneratorConfiguration configuration)
    {
        _builder = builder;
        _configuration = configuration;
    }

    public XmlQualifiedName Resolve(
        TypeModel typeModel,
        XmlSchemaParticle xmlParticle,
        XmlSchemaElementEx element)
    {
        // 1) Standard-Name
        var qn = element.ElementSchemaType.QualifiedName;

        // 2) Fallback auf refName
        if (qn.IsEmpty)
            qn = element.RefName;

        // 3) Anonymer Typ
        if (qn.IsEmpty)
        {
            var baseName = xmlParticle is XmlSchemaGroupRef grp
                ? grp.RefName.Name
                : typeModel.XmlSchemaName.Name;

            var candidate = _configuration.NamingProvider
                .TypeNameFromElement(baseName, element.QualifiedName.Name, element);

            qn = new XmlQualifiedName(candidate, typeModel.XmlSchemaName.Namespace);
        }

        // 4) Kollision abfangen über den Builder-Cache
        if (QualifiedNameResolver.NameExists(_builder._set, qn))
        {
            foreach (var suffix in new[] { "Item", "Property", "Element" })
            {
                var trial = new XmlQualifiedName(qn.Name + suffix, qn.Namespace);
                if (!QualifiedNameResolver.NameExists(_builder._set, trial))
                {
                    qn = trial;
                    break;
                }
            }
        }

        return qn;
    }
}