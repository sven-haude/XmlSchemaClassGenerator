using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using XmlSchemaClassGenerator.Model;
using XmlSchemaClassGenerator.Utils;

namespace XmlSchemaClassGenerator.Builder
{
    internal static class RootClassHandler
    {
        public static void ProcessElement(
            ModelBuilder builder,
            GeneratorConfiguration configuration,
            XmlSchemaElement rootElement,
            Dictionary<XmlQualifiedName, HashSet<Substitute>> substitutionGroups,
            Dictionary<string, TypeModel> types)
        {
            var qualifiedName = rootElement.ElementSchemaType.QualifiedName;
            if (qualifiedName.IsEmpty)
                qualifiedName = rootElement.QualifiedName;

            var type = builder.CreateTypeModel(qualifiedName, rootElement.ElementSchemaType);
            ClassModel derivedClassModel = null;

            if (type.RootElementName != null || type.IsAbstractRoot)
            {
                if (type is ClassModel cm)
                    derivedClassModel = CreateDerivedRootClass(builder, configuration, rootElement, type, cm, substitutionGroups);
                else
                    builder.SetType(rootElement, rootElement.QualifiedName, type);
            }
            else
            {
                if (type is ClassModel cm)
                    cm.Documentation.AddRange( DocumentationExtractor.GetDocumentation(rootElement));

                type.RootElement = rootElement;
                type.RootElementName = rootElement.QualifiedName;
            }

            if (!rootElement.SubstitutionGroup.IsEmpty)
            {
                if (!substitutionGroups.TryGetValue(rootElement.SubstitutionGroup, out var subs))
                {
                    subs = new HashSet<Substitute>();
                    substitutionGroups.Add(rootElement.SubstitutionGroup, subs);
                }
                subs.Add(new Substitute { Element = rootElement, Type = derivedClassModel ?? type });
            }
        }

        private static ClassModel CreateDerivedRootClass(
            ModelBuilder builder,
            GeneratorConfiguration configuration,
            XmlSchemaElement rootElement,
            TypeModel type,
            ClassModel classModel, Dictionary<XmlQualifiedName, HashSet<Substitute>> substitutionGroups)
        {
            var elementSource = CodeUtilities.CreateUri(rootElement.SourceUri);
            var derived = new ClassModel(configuration)
            {
                Name = configuration.NamingProvider.RootClassNameFromQualifiedName(rootElement.QualifiedName, rootElement),
                Namespace = builder.CreateNamespaceModel(elementSource, rootElement.QualifiedName)
            };
            derived.Documentation.AddRange(DocumentationExtractor.GetDocumentation(rootElement));

            if (derived.Namespace != null)
            {
                derived.Name = derived.Namespace.GetUniqueTypeName(derived.Name);
                derived.Namespace.Types[derived.Name] = derived;
            }

            builder.SetType(rootElement, rootElement.QualifiedName, derived);
            derived.BaseClass = classModel;
            classModel.DerivedTypes.Add(derived);
            derived.RootElementName = rootElement.QualifiedName;

            if (!type.IsAbstractRoot)
                CreateOriginalRootClass(builder, configuration, rootElement, type, classModel, substitutionGroups);

            return derived;
        }

        private static void CreateOriginalRootClass(
            ModelBuilder builder,
            GeneratorConfiguration configuration,
            XmlSchemaElement rootElement,
            TypeModel type,
            ClassModel classModel,
            Dictionary<XmlQualifiedName, HashSet<Substitute>> substitutionGroups)
        {
            var original = new ClassModel(configuration)
            {
                Name = configuration.NamingProvider.RootClassNameFromQualifiedName(type.RootElementName, rootElement),
                Namespace = classModel.Namespace
            };
            original.Documentation.AddRange(classModel.Documentation);
            classModel.Documentation.Clear();

            if (original.Namespace != null)
            {
                original.Name = original.Namespace.GetUniqueTypeName(original.Name);
                original.Namespace.Types[original.Name] = original;
            }

            if (classModel.XmlSchemaName?.IsEmpty == false)
                builder.SetType(classModel.RootElement, classModel.XmlSchemaName, original);

            original.BaseClass = classModel;
            classModel.DerivedTypes.Add(original);
            original.RootElementName = type.RootElementName;

            if (classModel.RootElement.SubstitutionGroup != null
                && substitutionGroups.TryGetValue(classModel.RootElement.SubstitutionGroup, out var subs))
            {
                foreach (var sub in subs.Where(s => s.Element == classModel.RootElement))
                    sub.Type = original;
            }

            classModel.RootElementName = null;
            classModel.IsAbstractRoot = true;
        }
    }
}