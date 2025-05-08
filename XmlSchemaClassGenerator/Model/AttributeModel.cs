using System.Collections.Generic;

namespace XmlSchemaClassGenerator;

    /// <summary>Repräsentiert ein einzelnes Attribut, das beim Code-Output angefügt werden kann.</summary>
    public sealed class AttributeModel
    {
        public string Name { get; set; }
        public IDictionary<string, object>? Arguments { get; set; }

        public AttributeModel(string name)
        {
            Name = name;
        }
    }
    
