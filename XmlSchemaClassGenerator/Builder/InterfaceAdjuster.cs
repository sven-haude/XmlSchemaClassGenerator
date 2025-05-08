using System.Collections.Generic;
using System.Linq;
using XmlSchemaClassGenerator.Model;

namespace XmlSchemaClassGenerator.Builder
{
    internal static class InterfaceAdjuster
    {
        public static void Apply(
            GeneratorConfiguration configuration,
            Dictionary<string, TypeModel> types)
        {
            // 1) Collection-/Required-Propagierung
            foreach (var interfaceModel in types.Values.OfType<InterfaceModel>())
            {
                foreach (var interfaceProperty in interfaceModel.Properties)
                {
                    var derivedProperties = interfaceModel.AllDerivedReferenceTypes().SelectMany(t => t.Properties)
                        .Where(p => p.Name == interfaceProperty.Name || p.OriginalPropertyName == interfaceProperty.Name).ToList();

                    if (derivedProperties.Exists(p => p.IsCollection))
                    {
                        foreach (var derivedProperty in derivedProperties.Where(p => !p.IsCollection))
                            derivedProperty.IsCollection = true;

                        interfaceProperty.IsCollection = true;
                    }
                    else
                    {
                        interfaceProperty.IsCollection = false;
                    }

                    if (derivedProperties.Exists(p => !p.IsRequired))
                    {
                        foreach (var derivedProperty in derivedProperties.Where(p => !p.IsRequired))
                            derivedProperty.IsRequired = false;

                        interfaceProperty.IsRequired = false;
                    }
                    else
                    {
                        interfaceProperty.IsRequired = true;
                    }
                }
            }

            // 2) Umbenennung, falls Implementierungsklassen Properties umbenennen
            foreach (var interfaceModel in types.Values.OfType<InterfaceModel>())
            {
                foreach (var interfaceProperty in interfaceModel.Properties)
                {
                    foreach (var implementationClass in interfaceModel.AllDerivedReferenceTypes())
                    {
                        foreach (var implementationClassProperty in implementationClass.Properties)
                        {
                            if (implementationClassProperty.Name != implementationClassProperty.OriginalPropertyName
                                && implementationClassProperty.OriginalPropertyName == interfaceProperty.Name
                                && implementationClassProperty.XmlSchemaName == interfaceProperty.XmlSchemaName
                                && implementationClassProperty.IsAttribute == interfaceProperty.IsAttribute)
                            {
                                
                                foreach (var derivedClass in interfaceModel.AllDerivedReferenceTypes().Where(c => c != implementationClass))
                                {
                                    foreach (var propertyModel in derivedClass.Properties.Where(p =>
                                                 implementationClassProperty.OriginalPropertyName == p.OriginalPropertyName
                                                 && implementationClassProperty.XmlSchemaName == p.XmlSchemaName
                                                 && implementationClassProperty.IsAttribute == p.IsAttribute))
                                        propertyModel.Name = implementationClassProperty.Name;
                                }
                                
                                interfaceProperty.Name = implementationClassProperty.Name;
                            }
                        }
                    }
                }
            }
            
            
            foreach (var iface in types.Values.OfType<InterfaceModel>())
            {
                foreach (var prop in iface.Properties)
                {
                    foreach (var implClass in iface.AllDerivedReferenceTypes())
                    {
                        foreach (var implProp in implClass.Properties)
                        {
                            if (implProp.Name != implProp.OriginalPropertyName
                                && implProp.OriginalPropertyName == prop.Name
                                && implProp.XmlSchemaName == prop.XmlSchemaName
                                && implProp.IsAttribute == prop.IsAttribute)
                            {
                                // In allen anderen Derived-Classes umbenennen
                                foreach (var other in iface.AllDerivedReferenceTypes().Where(c => c != implClass))
                                {
                                    other.Properties
                                         .Where(p => p.OriginalPropertyName == implProp.OriginalPropertyName
                                                  && p.XmlSchemaName == implProp.XmlSchemaName
                                                  && p.IsAttribute == implProp.IsAttribute)
                                         .ToList()
                                         .ForEach(p => p.Name = implProp.Name);
                                }
                                prop.Name = implProp.Name;
                            }
                        }
                    }
                }
            }

            // 3) Dubletten in Interfaces entfernen
            
            foreach (var interfaceModel in types.Values.OfType<InterfaceModel>())
            {
                var parentProperties = interfaceModel.Properties.ToList();
                foreach (var baseInterfaceTypeProperties in interfaceModel.AllDerivedReferenceTypes().OfType<InterfaceModel>().Select(i => i.Properties))
                {
                    foreach (var parentProperty in parentProperties)
                    {
                        var baseProperties = baseInterfaceTypeProperties.ToList();
                        foreach (var baseProperty in baseProperties.Where(baseProperty => parentProperty.Name == baseProperty.Name && parentProperty.Type.Name == baseProperty.Type.Name))
                            baseInterfaceTypeProperties.Remove(baseProperty);
                    }
                }
            }
            
            // foreach (var iface in types.Values.OfType<InterfaceModel>())
            // {
            //     var parentProps = iface.Properties.ToList();
            //     foreach (var derived in iface.AllDerivedReferenceTypes().OfType<InterfaceModel>())
            //     {
            //         foreach (var dup in parentProps
            //             .SelectMany(pp => derived.Properties
            //                 .Where(dp => dp.Name == pp.Name && dp.Type.Name == pp.Type.Name)))
            //         {
            //             derived.Properties.Remove(dup);
            //         }
            //     }
            // }
        }
    }
}