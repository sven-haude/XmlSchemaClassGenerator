using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using XmlSchemaClassGenerator.Model;
using XmlSchemaClassGenerator.Utils;

namespace XmlSchemaClassGenerator.Builder;

internal class ModelBuilder
{

    private readonly GeneratorConfiguration _configuration;
    internal readonly XmlSchemaSet _set;
    internal readonly Dictionary<XmlQualifiedName, HashSet<XmlSchemaAttributeGroup>> AttributeGroups = [];
    internal readonly Dictionary<XmlQualifiedName, HashSet<XmlSchemaGroup>> Groups = [];
    internal readonly Dictionary<NamespaceKey, NamespaceModel> Namespaces = [];
    internal readonly Dictionary<string, TypeModel> Types = [];
    private readonly Dictionary<XmlQualifiedName, HashSet<Substitute>> SubstitutionGroups = [];

    internal static readonly XmlQualifiedName AnyType = new("anyType", XmlSchema.Namespace);

    internal static string BuildKey(XmlSchemaAnnotated annotated, XmlQualifiedName name)
        => $"{annotated.GetType()}:{annotated.SourceUri}:{annotated.LineNumber}:{annotated.LinePosition}:{name}";

    internal void SetType(XmlSchemaAnnotated annotated, XmlQualifiedName name, TypeModel type)
        => Types[BuildKey(annotated, name)] = type;

   public ModelBuilder(GeneratorConfiguration configuration, XmlSchemaSet set)
    {
        _configuration = configuration;
        _set = set;

        // Disable comments if requested
        GeneratorModel.DisableComments = _configuration.DisableComments;

        // Seed the AnyType model
        InitializeAnyType();

        // 1) Resolve schemas in dependency order
        var dependencyOrder = DependencyResolver.Resolve(_set);

        // 2) Populate AttributeGroups and Groups via our SchemaIndexer
        SchemaIndexer.IndexSchemas(_set, AttributeGroups, Groups);

        // 3) Create all global types and process root elements in schema order
        foreach (var schema in dependencyOrder)
        {
            ProcessGlobalTypes(schema);
            foreach (var rootElement in _set.GlobalElements.Values
                     .Cast<XmlSchemaElement>()
                     .Where(e => e.GetSchema() == schema))
            {
                RootClassHandler.ProcessElement(
                    this,
                    _configuration,
                    rootElement,
                    SubstitutionGroups,
                    Types);
            }
        }

        // 4) Process substitution groups
        SubstitutionHandler.ProcessSubstitutes(this, _configuration);
        
        // 5) Interface-related adjustments
        if (_configuration.GenerateInterfaces)
            InterfaceAdjuster.Apply(_configuration, Types);

        // 6) Final cleanup steps
        CleanupHandler.Apply(Types);

        // 7) Ensure unique type names across namespaces if required
        if (_configuration.UniqueTypeNameAcrossNamespaces)
            UniqueNameResolver.Apply(Namespaces.Values);
        
        // Bau den Resolver und gib ihm direkten Zugriff auf NameExists()
        _qualifiedNameResolver = new DefaultQualifiedNameResolver(this, _configuration);
    }
   
    private readonly IQualifiedNameResolver _qualifiedNameResolver;
    
    internal XmlQualifiedName GetQualifiedName(
        TypeModel typeModel,
        XmlSchemaParticle xmlParticle,
        XmlSchemaElementEx element)
        => _qualifiedNameResolver.Resolve(typeModel, xmlParticle, element);

    private void ProcessGlobalTypes(XmlSchema schema)
    {
        foreach (var globalType in _set.GlobalTypes.Values
                     .Cast<XmlSchemaType>()
                     .Where(t => t.GetSchema() == schema))
        {
            CreateTypeModel(globalType.QualifiedName, globalType);
        }
    }

    internal IEnumerable<Substitute> GetSubstitutedElements(XmlQualifiedName name)
    {
        if (SubstitutionGroups.TryGetValue(name, out var substitutes))
        {
            foreach (var substitute in substitutes.Where(s => s.Element.QualifiedName != name))
            {
                yield return substitute;
                foreach (var recursiveSubstitute in GetSubstitutedElements(substitute.Element.QualifiedName))
                    yield return recursiveSubstitute;
            }
        }
    }

    internal TypeModel CreateTypeModel(XmlQualifiedName qualifiedName, XmlSchemaAnnotated type)
    {
        var key = BuildKey(type, qualifiedName);
        if (!qualifiedName.IsEmpty && Types.TryGetValue(key, out TypeModel typeModel)) return typeModel;

        var source = CodeUtilities.CreateUri(type.SourceUri);
        var namespaceModel = CreateNamespaceModel(source, qualifiedName);
        var docs = DocumentationExtractor.GetDocumentation(type);

        var typeModelBuilder = new TypeModelBuilder(this, _configuration, qualifiedName, namespaceModel, docs, source);

        return typeModelBuilder.Create(type);
    }
    internal NamespaceModel CreateNamespaceModel(Uri source, XmlQualifiedName qualifiedName)
    {
        NamespaceModel namespaceModel = null;
        if (!qualifiedName.IsEmpty && qualifiedName.Namespace != XmlSchema.Namespace)
        {
            var key = new NamespaceKey(source, qualifiedName.Namespace);
            if (!Namespaces.TryGetValue(key, out namespaceModel))
            {
                var namespaceName = BuildNamespace(source, qualifiedName.Namespace);
                namespaceModel = new NamespaceModel(key, _configuration) { Name = namespaceName };
                Namespaces.Add(key, namespaceModel);
            }
        }
        return namespaceModel;
    }

    public IEnumerable<CodeNamespace> GenerateCode()
    {
        var hierarchy = NamespaceHierarchyItem.Build(Namespaces.Values.GroupBy(x => x.Name).SelectMany(x => x))
            .MarkAmbiguousNamespaceTypes();
        return hierarchy.Flatten()
            .Select(nhi => NamespaceModel.Generate(nhi.FullName, nhi.Models, _configuration));
    }

    private string BuildNamespace(Uri source, string xmlNamespace)
        => NamespaceResolver.GetNamespaceName(_configuration, source, xmlNamespace);

    private void InitializeAnyType()
    {
        var objectModel = new SimpleModel(_configuration)
        {
            Name = "AnyType",
            Namespace = CreateNamespaceModel(new Uri(XmlSchema.Namespace), AnyType),
            XmlSchemaName = AnyType,
            XmlSchemaType = null,
            ValueType = typeof(object),
            UseDataTypeAttribute = false
        };
        SetType(new XmlSchemaComplexType(), AnyType, objectModel);
    }
}
