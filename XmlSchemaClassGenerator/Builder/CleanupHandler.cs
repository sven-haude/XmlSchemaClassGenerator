using System.Collections.Generic;
using System.Linq;
using XmlSchemaClassGenerator.Model;

namespace XmlSchemaClassGenerator.Builder
{
    internal static class CleanupHandler
    {
        public static void Apply(Dictionary<string, TypeModel> types)
        {
            RemoveDuplicatesInDerivedClasses(types);
            AddXmlRootAttributes(types);
        }

        private static void RemoveDuplicatesInDerivedClasses(Dictionary<string, TypeModel> types)
        {
            foreach (var classModel in types.Values.OfType<ClassModel>())
            {
                var baseProps = classModel.AllBaseClasses
                    .SelectMany(b => b.Properties)
                    .ToList();
                foreach (var propToRemove in classModel.Properties
                             .Where(p => p.XmlSchemaName != null && baseProps.Any(bp => bp.XmlSchemaName == p.XmlSchemaName))
                             .ToList())
                {
                    classModel.Properties.Remove(propToRemove);
                }
            }
        }

        private static void AddXmlRootAttributes(Dictionary<string, TypeModel> types)
        {
            var ambiguousGroups = types.Values
                .Where(t => t.RootElementName == null && !t.IsAbstractRoot && t is not InterfaceModel)
                .GroupBy(t => t.Name);

            foreach (var group in ambiguousGroups)
            {
                var list = group.ToList();
                if (list.Count < 2) 
                    continue;

                foreach (var typeModel in list)
                {
                    typeModel.RootElementName = typeModel.GetQualifiedName();
                }
            }
        }
    }
}