using System.CodeDom;
using System.Collections.Generic;
using System.Linq;

namespace XmlSchemaClassGenerator.Model;

public class NamespaceModel(NamespaceKey key, GeneratorConfiguration configuration) : GeneratorModel(configuration)
{
    public string Name { get; set; }
    public NamespaceKey Key { get; } = key;
    public Dictionary<string, TypeModel> Types { get; set; } = [];
    /// <summary>
    /// Does the namespace of this type clashes with a class in the same or upper namespace?
    /// </summary>
    public bool IsAmbiguous { get; set; }
    
    /// <summary>Alle Choice-Enum-Definitionen, die für diesen Typ erzeugt wurden.</summary>
    public List<ChoiceEnumDefinition> ChoiceEnumDefinitions { get; } = new List<ChoiceEnumDefinition>();

    /// <summary>Zähler für erzeugte Choice-Properties, um eindeutige Suffixe zu bilden.</summary>
    public int ChoicePropertiesCount { get; set; } = 0;

    public static CodeNamespace Generate(string namespaceName, IEnumerable<NamespaceModel> parts, GeneratorConfiguration conf)
    {
        var codeNamespace = new CodeNamespace(namespaceName);

        foreach (var (Namespace, _) in CodeUtilities.UsingNamespaces.Where(n => n.Condition(conf)).OrderBy(n => n.Namespace))
            codeNamespace.Imports.Add(new CodeNamespaceImport(Namespace));

        foreach (var typeModel in parts.SelectMany(x => x.Types.Values).ToList())
        {
            var type = typeModel.Generate();
            if (type != null)
            {
                codeNamespace.Types.Add(type);
            }
        }

        return codeNamespace;
    }
}