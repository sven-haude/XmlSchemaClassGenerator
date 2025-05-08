using System.Collections.Generic;
using System.Xml;

namespace XmlSchemaClassGenerator.Model;

public class EnumValueModel
{
    public EnumValueModel()
    {
        
    }
    public EnumValueModel(string name, XmlQualifiedName qname)
    {
        Name = name;
        XmlName = qname;
    }
    public string Name { get; set; }
    public string Value { get; set; }
    public bool IsDeprecated { get; set; }
    
    public XmlQualifiedName XmlName { get; }
    public List<DocumentationModel> Documentation { get; } = [];
}