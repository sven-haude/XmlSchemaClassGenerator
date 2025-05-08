using System.Linq;
using XmlSchemaClassGenerator.Model;

namespace XmlSchemaClassGenerator.Builder
{
    internal static class SubstitutionHandler
    {
        public static void ProcessSubstitutes(
            ModelBuilder builder,
            GeneratorConfiguration configuration)
        {
            var classesProps = builder.Types.Values.OfType<ClassModel>().Select(c => c.Properties.ToList()).ToList();

            foreach (var classProps in classesProps)
            {
                var order = 0;

                foreach (var prop in classProps)
                {
                    if (configuration.EmitOrder)
                    {
                        prop.Order = order;
                        order++;
                    }

                    if (prop.XmlSchemaName != null)
                    {
                        var substitutes = builder.GetSubstitutedElements(prop.XmlSchemaName);

                        if (configuration.SeparateSubstitutes)
                        {
                            foreach (var substitute in substitutes)
                            {
                                var cls = (ClassModel)prop.OwningType;
                                var schema = substitute.Element.GetSchema();
                                var source = CodeUtilities.CreateUri(schema.SourceUri);
                                var props = PropertyFactory.CreatePropertiesForElements(builder, configuration, source, cls, prop.Particle, [prop.Particle], substitute, order);

                                cls.Properties.AddRange(props);

                                order += props.Count();
                            }
                        }
                        else
                        {
                            prop.Substitutes.AddRange(substitutes);
                        }
                    }
                }
            }
        }
    }
}