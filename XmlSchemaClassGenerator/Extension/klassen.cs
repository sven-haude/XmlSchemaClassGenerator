using System.Collections.Generic;

namespace XmlSchemaClassGenerator.Model
{
    /// <summary>Eine Zuordnung für ein mögliches Element in einer xs:choice-Property.</summary>
    public class XmlElementMapping
    {
        public string ElementName      { get; set; }
        public string ElementNamespace { get; set; }
        public TypeModel DataType      { get; set; }
    }

    /// <summary>Ein Enum-Mitglied inkl. des XML-Werts, der serialisiert wird.</summary>
    public class EnumMemberModel
    {
        public string Name     { get; set; }   // C#-Bezeichner
        public string XmlValue { get; set; }   // Wert, der in XmlEnum("…") landet
    }
}

