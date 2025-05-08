using System.Collections.Generic;
using System.Xml;
using XmlSchemaClassGenerator.Model;

namespace XmlSchemaClassGenerator
{
    /// <summary>Daten zu einem einzelnen Element der Choice-Alternativen.</summary>
    public class ChoiceElementInfo
    {
        public XmlQualifiedName ElementName { get; set; }
        public TypeModel        ElementType { get; set; }
        public bool             IsCollection { get; set; }
    }

    /// <summary>Definition eines Enums für eine Choice-Gruppe.</summary>
    public sealed class ChoiceEnumDefinition
    {
        public string               Name    { get; }
        public List<EnumValueModel> Members { get; }

        public ChoiceEnumDefinition(string name, List<EnumValueModel> members)
        {
            Name    = name;
            Members = members;
        }
    }
}