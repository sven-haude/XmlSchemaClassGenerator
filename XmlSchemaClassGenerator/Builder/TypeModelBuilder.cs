using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using XmlSchemaClassGenerator.Model;
using XmlSchemaClassGenerator.Utils;

namespace XmlSchemaClassGenerator.Builder
{
    internal sealed class TypeModelBuilder
    {
        private readonly ModelBuilder builder;
        private readonly GeneratorConfiguration configuration;
        private readonly XmlQualifiedName qualifiedName;
        private readonly NamespaceModel namespaceModel;
        private readonly List<DocumentationModel> docs;
        private readonly Uri source;
        private int _choiceIndex;   // läuft für mehrere Choice-Blöcke hoch

        public TypeModelBuilder(
            ModelBuilder builder,
            GeneratorConfiguration configuration,
            XmlQualifiedName qualifiedName,
            NamespaceModel namespaceModel,
            List<DocumentationModel> docs,
            Uri source)
        {
            this.builder = builder;
            this.configuration = configuration;
            this.qualifiedName = qualifiedName;
            this.namespaceModel = namespaceModel;
            this.docs = docs;
            this.source = source;
        }

        public TypeModel Create(XmlSchemaAnnotated type) => type switch
        {
            XmlSchemaGroup group => CreateTypeModel(group),
            XmlSchemaAttributeGroup attrGroup => CreateTypeModel(attrGroup),
            XmlSchemaComplexType complexType => CreateTypeModel(complexType),
            XmlSchemaSimpleType simpleType => CreateTypeModel(simpleType),
            _ => throw new NotSupportedException($"Cannot build declaration for {qualifiedName}")
        };

        // Hier die bisherigen privaten CreateTypeModel-Überladungen 
        // und alle Hilfsmethoden 1:1 einfügen, dabei Felder anpassen:
        // configuration → configuration, builder → this.builder usw.
        
         private TypeModel CreateTypeModel(XmlSchemaGroup group)
        {
            var name = "I" + configuration.NamingProvider.GroupTypeNameFromQualifiedName(qualifiedName, group);

            InterfaceModel interfaceModel = CreateInterfaceModel(group, name);

            var xmlParticle = group.Particle;
            var particle = new Particle(xmlParticle, group.Parent);
            var items = ParticleExtractor.GetElements(xmlParticle);
            var properties = PropertyFactory.CreatePropertiesForElements(builder, configuration, source, interfaceModel, particle, items.Where(i => i.XmlParticle is not XmlSchemaGroupRef));
            interfaceModel.Properties.AddRange(properties);
            AddInterfaces(interfaceModel, items);

            return interfaceModel;
        }
         
        private void AddInterfaces(ReferenceTypeModel refTypeModel, IEnumerable<Particle> items)
        {
            var interfaces = items.Select(i => i.XmlParticle).OfType<XmlSchemaGroupRef>()
                .Select(i => (InterfaceModel)builder.CreateTypeModel(i.RefName, builder.Groups[i.RefName].First()));
            refTypeModel.AddInterfaces(interfaces);
        }

        private void AddInterfaces(ReferenceTypeModel refTypeModel, XmlSchemaObjectCollection attributes)
        {
            var interfaces = attributes.OfType<XmlSchemaAttributeGroupRef>()
                .Select(a => (InterfaceModel)builder.CreateTypeModel(a.RefName, builder.AttributeGroups[a.RefName].First()));
            refTypeModel.AddInterfaces(interfaces);
        }
         
        private InterfaceModel CreateInterfaceModel(XmlSchemaAnnotated group, string name)
        {
            if (namespaceModel != null)
                name = namespaceModel.GetUniqueTypeName(name);

            var interfaceModel = new InterfaceModel(configuration)
            {
                Name = name,
                Namespace = namespaceModel,
                XmlSchemaName = qualifiedName
            };

            interfaceModel.Documentation.AddRange(docs);

            if (namespaceModel != null)
                namespaceModel.Types[name] = interfaceModel;

            if (!qualifiedName.IsEmpty)
                builder.SetType(group, qualifiedName, interfaceModel);
            return interfaceModel;
        }

        private TypeModel CreateTypeModel(XmlSchemaAttributeGroup group)
        {
            var name = "I" + configuration.NamingProvider.AttributeGroupTypeNameFromQualifiedName(qualifiedName, group);

            InterfaceModel interfaceModel = CreateInterfaceModel(group, name);

            var attributes = group.Attributes;
            var properties = PropertyFactory.CreatePropertiesForAttributes(builder, configuration, source, interfaceModel, attributes.OfType<XmlSchemaAttribute>());
            interfaceModel.Properties.AddRange(properties);
            AddInterfaces(interfaceModel, attributes);

            return interfaceModel;
        }

         private IEnumerable<PropertyModel> CreatePropertiesForAttributes(Uri source, TypeModel owningTypeModel, IEnumerable<XmlSchemaObject> items)
    {
        var properties = new List<PropertyModel>();

        foreach (var item in items)
        {
            switch (item)
            {
                case XmlSchemaAttribute attribute when attribute.Use != XmlSchemaUse.Prohibited:

                    properties.Add(PropertyFromAttribute(owningTypeModel, attribute, properties));
                    break;

                case XmlSchemaAttributeGroupRef attributeGroupRef:

                    foreach (var attributeGroup in builder.AttributeGroups[attributeGroupRef.RefName])
                    {
                        if (configuration.GenerateInterfaces)
                            CreateTypeModel(attributeGroupRef.RefName, attributeGroup);

                        var attributes = attributeGroup.Attributes.Cast<XmlSchemaObject>()
                            .Where(a => !(a is XmlSchemaAttributeGroupRef agr && agr.RefName == attributeGroupRef.RefName))
                            .ToList();

                        if (attributeGroup.RedefinedAttributeGroup != null)
                        {
                            var attrs = attributeGroup.RedefinedAttributeGroup.Attributes.Cast<XmlSchemaObject>()
                                .Where(a => !(a is XmlSchemaAttributeGroupRef agr && agr.RefName == attributeGroupRef.RefName)).ToList();

                            foreach (var attr in attrs)
                            {
                                var n = attr.GetQualifiedName();

                                if (n != null)
                                    attributes.RemoveAll(a => a.GetQualifiedName() == n);

                                attributes.Add(attr);
                            }
                        }

                        var newProperties = CreatePropertiesForAttributes(source, owningTypeModel, attributes);
                        properties.AddRange(newProperties);
                    }

                    break;
            }
        }
        return properties;
    }
         
          private PropertyModel PropertyFromAttribute(TypeModel owningTypeModel, XmlSchemaAttributeEx attribute, IList<PropertyModel> properties)
    {
        var attributeQualifiedName = attribute.AttributeSchemaType.QualifiedName;
        var name = configuration.NamingProvider.AttributeNameFromQualifiedName(attribute.QualifiedName, attribute);
        var originalName = name;

        if (attribute.Base.Parent is XmlSchemaAttributeGroup attributeGroup
            && attributeGroup.QualifiedName != owningTypeModel.XmlSchemaName
            && builder.Types.TryGetValue(ModelBuilder.BuildKey(attributeGroup, attributeGroup.QualifiedName), out var typeModelValue)
            && typeModelValue is InterfaceModel interfaceTypeModel)
        {
            var interfaceProperty = interfaceTypeModel.Properties.Single(p => p.XmlSchemaName == attribute.QualifiedName);
            attributeQualifiedName = interfaceProperty.Type.XmlSchemaName;
            name = interfaceProperty.Name;
        }
        else
        {
            if (attributeQualifiedName.IsEmpty)
            {
                attributeQualifiedName = attribute.QualifiedName;

                if (attributeQualifiedName.IsEmpty || string.IsNullOrEmpty(attributeQualifiedName.Namespace))
                {
                    // inner type, have to generate a type name
                    var typeName = configuration.NamingProvider.TypeNameFromAttribute(owningTypeModel.Name, attribute.QualifiedName.Name, attribute);
                    attributeQualifiedName = new XmlQualifiedName(typeName, owningTypeModel.XmlSchemaName.Namespace);
                    // try to avoid name clashes
                    if (QualifiedNameResolver.NameExists(builder._set, attributeQualifiedName))
                        attributeQualifiedName = new[] { Constants.ItemName, Constants.PropertyName, Constants.ElementName }.Select(s => new XmlQualifiedName(attributeQualifiedName.Name + s, attributeQualifiedName.Namespace)).First(n => ! QualifiedNameResolver.NameExists(builder._set, n));
                }
            }

            if (name == owningTypeModel.Name)
                name += Constants.PropertyName;
        }

        name = owningTypeModel.GetUniquePropertyName(name, properties);

        var typeModel = CreateTypeModel(attributeQualifiedName, attribute.AttributeSchemaType);
        var property = new PropertyModel(configuration, name, typeModel, owningTypeModel)
        {
            IsAttribute = true,
            IsRequired = attribute.Use == XmlSchemaUse.Required
        };

        property.SetFromNode(originalName, attribute.Use != XmlSchemaUse.Optional, attribute);
        property.SetSchemaNameAndNamespace(owningTypeModel, attribute);
        property.Documentation.AddRange(DocumentationExtractor.GetDocumentation(attribute));

        return property;
    }

    private TypeModel CreateTypeModel(XmlQualifiedName qualifiedName, XmlSchemaAnnotated type)
    {
        var key = ModelBuilder.BuildKey(type, qualifiedName);
        if (!qualifiedName.IsEmpty && builder.Types.TryGetValue(key, out TypeModel typeModel)) return typeModel;

        var source = CodeUtilities.CreateUri(type.SourceUri);
        var namespaceModel = CreateNamespaceModel(source, qualifiedName);
        var docs = DocumentationExtractor.GetDocumentation(type);

        var typeModelBuilder = new TypeModelBuilder(builder, configuration, qualifiedName, namespaceModel, docs, source);

        return typeModelBuilder.Create(type);
    }

        private TypeModel CreateTypeModel(XmlSchemaComplexType complexType)
        {
            var name = configuration.NamingProvider.ComplexTypeNameFromQualifiedName(qualifiedName, complexType);
            if (namespaceModel != null)
                name = namespaceModel.GetUniqueTypeName(name);

            var classModel = new ClassModel(configuration)
            {
                Name = name,
                Namespace = namespaceModel,
                XmlSchemaName = qualifiedName,
                XmlSchemaType = complexType,
                IsAbstract = complexType.IsAbstract,
                IsAnonymous = string.IsNullOrEmpty(complexType.QualifiedName.Name),
                IsMixed = complexType.IsMixed,
                IsSubstitution = complexType.Parent is XmlSchemaElement parent && !parent.SubstitutionGroup.IsEmpty
            };

            classModel.Documentation.AddRange(docs);

            if (namespaceModel != null)
                namespaceModel.Types[classModel.Name] = classModel;

            if (!qualifiedName.IsEmpty)
                builder.SetType(complexType, qualifiedName, classModel);

            if (complexType.BaseXmlSchemaType != null && complexType.BaseXmlSchemaType.QualifiedName != ModelBuilder.AnyType)
            {
                var baseModel = builder.CreateTypeModel(complexType.BaseXmlSchemaType.QualifiedName, complexType.BaseXmlSchemaType);
                classModel.BaseClass = baseModel;
                if (baseModel is ClassModel baseClassModel)
                {
                    baseClassModel.DerivedTypes.Add(classModel);
                    if (classModel.AllBaseTypes.Any(b => b.XmlSchemaType.QualifiedName == qualifiedName))
                        classModel.Name += "Redefinition";
                }
            }

            XmlSchemaParticle xmlParticle = null;
            if (classModel.BaseClass != null)
            {
                if (complexType.ContentModel.Content is XmlSchemaComplexContentExtension complexContent)
                    xmlParticle = complexContent.Particle;

                // If it's a restriction, do not duplicate elements on the derived class, they're already in the base class.
                // See https://msdn.microsoft.com/en-us/library/f3z3wh0y.aspx
            }
            else
            {
                xmlParticle = complexType.Particle ?? complexType.ContentTypeParticle;
            }
            
            // ---------------------------------------------------------------------
            // xs:choice speziell behandeln – sonst wie gehabt
            // ---------------------------------------------------------------------
            // 2.  Aufruf an der einen Stelle, wo du das Root-Particle hast
            if (xmlParticle != null)
                CollectChoices(xmlParticle, classModel);
            // if (xmlParticle is XmlSchemaChoice rootChoice)
            // {
            //     AddChoiceProperty(classModel, rootChoice);
            // }
            else
            {
                var items = ParticleExtractor.GetElements(xmlParticle, complexType).ToList();

                if (configuration.GenerateInterfaces)
                    AddInterfaces(classModel, items);

                var particle   = new Particle(xmlParticle, xmlParticle?.Parent);
                var properties = PropertyFactory.CreatePropertiesForElements(
                    builder, configuration, source, classModel,
                    particle, items);
                classModel.Properties.AddRange(properties);
            }

            
            
            
            
            // var items = ParticleExtractor.GetElements(xmlParticle, complexType).ToList();
            //
            // if (configuration.GenerateInterfaces) AddInterfaces(classModel, items);
            //
            // var particle = new Particle(xmlParticle, xmlParticle?.Parent);
            // var properties = PropertyFactory.CreatePropertiesForElements(builder, configuration, source, classModel, particle, items);
            // classModel.Properties.AddRange(properties);
            
            
            
            
            

            XmlSchemaObjectCollection attributes = null;
            if (classModel.BaseClass != null)
            {
                if (complexType.ContentModel.Content is XmlSchemaComplexContentExtension complexContent)
                    attributes = complexContent.Attributes;
                else if (complexType.ContentModel.Content is XmlSchemaSimpleContentExtension simpleContent)
                    attributes = simpleContent.Attributes;

                // If it's a restriction, do not duplicate attributes on the derived class, they're already in the base class.
                // See https://msdn.microsoft.com/en-us/library/f3z3wh0y.aspx
            }
            else
            {
                attributes = complexType.Attributes;

                if (attributes.Count == 0 && complexType.ContentModel != null)
                {
                    var content = complexType.ContentModel.Content;

                    if (content is XmlSchemaComplexContentExtension extension)
                        attributes = extension.Attributes;
                    else if (content is XmlSchemaComplexContentRestriction restriction)
                        attributes = restriction.Attributes;
                }
            }

            if (attributes != null)
            {
                var attributeProperties = PropertyFactory.CreatePropertiesForAttributes(builder, configuration, source, classModel, attributes.Cast<XmlSchemaObject>());
                classModel.Properties.AddRange(attributeProperties);

                if (configuration.GenerateInterfaces)
                    AddInterfaces(classModel, attributes);
            }

            XmlSchemaAnyAttribute anyAttribute = null;
            if (complexType.AnyAttribute != null)
            {
                anyAttribute = complexType.AnyAttribute;
            }
            else if (complexType.AttributeWildcard != null)
            {
                var hasAnyAttribute = true;
                for (var baseType = complexType.BaseXmlSchemaType; baseType != null; baseType = baseType.BaseXmlSchemaType)
                {
                    if (baseType is not XmlSchemaComplexType baseComplexType)
                        continue;

                    if (baseComplexType.QualifiedName != ModelBuilder.AnyType && baseComplexType.AttributeWildcard != null)
                    {
                        hasAnyAttribute = false;
                        break;
                    }
                }

                if (hasAnyAttribute)
                    anyAttribute = complexType.AttributeWildcard;
            }

            if (anyAttribute != null)
            {
                SimpleModel type = new(configuration) { ValueType = typeof(XmlAttribute), UseDataTypeAttribute = false };
                var property = new PropertyModel(configuration, "AnyAttribute", type, classModel)
                {
                    IsAttribute = true,
                    IsCollection = true,
                    IsAny = true
                };

                var attributeDocs = DocumentationExtractor.GetDocumentation(anyAttribute);
                property.Documentation.AddRange(attributeDocs);

                classModel.Properties.Add(property);
            }

            return classModel;
        }
        
        NamespaceModel CreateNamespaceModel(Uri source, XmlQualifiedName qualifiedName)
        {
            NamespaceModel namespaceModel = null;
            if (!qualifiedName.IsEmpty && qualifiedName.Namespace != XmlSchema.Namespace)
            {
                var key = new NamespaceKey(source, qualifiedName.Namespace);
                if (!builder.Namespaces.TryGetValue(key, out namespaceModel))
                {
                    var namespaceName =  NamespaceResolver.GetNamespaceName(configuration, source, qualifiedName.Namespace);
                    namespaceModel = new NamespaceModel(key, configuration) { Name = namespaceName };
                    builder.Namespaces.Add(key, namespaceModel);
                }
            }
            return namespaceModel;
        }
        
        // 1.  Neu: rekursive Suche nach Choice-Blöcken
        private void CollectChoices(XmlSchemaParticle particle, ClassModel owningType)
        {
            if (particle is XmlSchemaChoice choice)
                AddChoiceProperty(owningType, choice);

            switch (particle)
            {
                case XmlSchemaSequence seq:
                    foreach (var p in seq.Items.OfType<XmlSchemaParticle>())
                        CollectChoices(p, owningType);
                    break;
                case XmlSchemaAll all:
                    foreach (var p in all.Items.OfType<XmlSchemaParticle>())
                        CollectChoices(p, owningType);
                    break;

                case XmlSchemaGroupRef groupRef:
                    var group = builder.Groups[groupRef.RefName].First();
                    CollectChoices(group.Particle, owningType);
                    break;

                // Weitere Fälle (XmlSchemaAny, etc.) nur bei Bedarf
            }
        }
        
        private void AddChoiceProperty(ClassModel owningTypeModel, XmlSchemaChoice choice)
{
    bool isMultiple = choice.MaxOccurs > 1 || choice.MaxOccursString == "unbounded";

    // Property-Namen wie bei xsd.exe
    int idx = _choiceIndex++;
    string baseName = isMultiple ? "Items" : "Item";
    string propertyName = idx == 0 ? baseName : $"{baseName}{idx}";

    // ------------------------------------------------
    //  Enum-Typ anlegen
    // ------------------------------------------------
    string enumTypeName = propertyName + "ChoiceType";
    var enumModel = new ClassModel(configuration)
    {
        Name          = enumTypeName,
        IsEnum        = true,
        IsChoiceEnum  = true
    };

    // Sammle Element-Infos für Property und Enum
    var elementMappings = new List<XmlElementMapping>();

    foreach (var opt in choice.Items.OfType<XmlSchemaElement>())
    {
        string xmlName = !opt.RefName.IsEmpty ? opt.RefName.Name : opt.Name;
        string xmlNs   = !opt.RefName.IsEmpty ? opt.RefName.Namespace : null;

        // .NET-Typ für das Element erzeugen / holen
        var clrType = builder.CreateTypeModel(opt.ElementSchemaType.QualifiedName,
                                              opt.ElementSchemaType);

        elementMappings.Add(new XmlElementMapping
        {
            ElementName      = xmlName,
            ElementNamespace = xmlNs,
            DataType         = clrType
        });

        // TODO: geht noch nicht. 
        // Enum-Member anlegen (Name eindeutig machen)
        string memberName = configuration.NamingProvider
            .EnumMemberNameFromValue(enumTypeName, xmlName, (XmlSchemaEnumerationFacet)null);
                            // .EnumMemberNameFromValue(enumTypeName, xmlName, xmlNs);
        enumModel.EnumMembers.Add(new EnumMemberModel
        {
            Name     = memberName,
            XmlValue = xmlName
        });
    }

    owningTypeModel.NestedTypes.Add(enumModel);

    // ------------------------------------------------
    //  Enum-Property
    // ------------------------------------------------
    string enumPropName = propertyName + "ElementName";
    var enumProperty = new PropertyModel( configuration, enumPropName, enumModel, owningTypeModel)
    {
        Name         = enumPropName,
      //  DataType     = enumModel,
        IsIgnored    = true,
        IsCollection = isMultiple
    };

    // ------------------------------------------------
    //  Haupt-Property (object / object[])
    // ------------------------------------------------
    var choiceProperty = new PropertyModel(configuration, propertyName, 
        new SimpleModel(configuration) { ValueType = typeof(object) }, owningTypeModel)
    {
        Name         = propertyName,
        //DataType     = new SimpleModel(configuration) { ValueType = typeof(object) },
        IsCollection = isMultiple
    };
    foreach (var map in elementMappings)
        choiceProperty.AddChoiceAlternative(map.ElementName, map.DataType, map.ElementNamespace);

    // ChoiceIdentifier verknüpfen
    choiceProperty.SetXmlChoiceIdentifier(enumProperty);

    // ------------------------------------------------
    //  Zur Klasse hinzufügen
    // ------------------------------------------------
    owningTypeModel.Properties.Add(choiceProperty);
    owningTypeModel.Properties.Add(enumProperty);
}


        private TypeModel CreateTypeModel(XmlSchemaSimpleType simpleType)
        {
            List<RestrictionModel> restrictions = null;
            List<IEnumerable<XmlSchemaFacet>> baseFacets = null;

            var facets = simpleType.Content switch
            {
                XmlSchemaSimpleTypeRestriction typeRestriction when !configuration.MergeRestrictionsWithBase => typeRestriction.Facets.Cast<XmlSchemaFacet>().ToList(),
                XmlSchemaSimpleTypeUnion typeUnion when AllMembersHaveFacets(typeUnion, out baseFacets) => baseFacets.SelectMany(f => f).ToList(),
                _ => MergeRestrictions(simpleType)
            };

            if (facets.Count > 0)
            {
                var enumFacets = facets.OfType<XmlSchemaEnumerationFacet>().ToList();

                // If a union has enum restrictions, there must be an enum restriction in all parts of the union
                // If there are other restrictions mixed into the enumeration values, we'll generate a string to play it safe.
                if (enumFacets.Count > 0 && (baseFacets is null || baseFacets.TrueForAll(fs => fs.OfType<XmlSchemaEnumerationFacet>().Any())) && !configuration.EnumAsString)
                    return CreateEnumModel(simpleType, enumFacets);

                restrictions = CodeUtilities.GetRestrictions(facets, simpleType, configuration).Where(r => r != null).Sanitize().ToList();
            }

            return CreateSimpleModel(simpleType, restrictions ?? []);

            static bool AllMembersHaveFacets(XmlSchemaSimpleTypeUnion typeUnion, out List<IEnumerable<XmlSchemaFacet>> baseFacets)
            {
                var members = typeUnion.BaseMemberTypes.Select(b => b.Content as XmlSchemaSimpleTypeRestriction);
                var retval = members.All(r => r?.Facets.Count > 0);
                baseFacets = !retval ? null : members.Select(r => r.Facets.Cast<XmlSchemaFacet>()).ToList();
                return retval;
            }

            static List<XmlSchemaFacet> MergeRestrictions(XmlSchemaSimpleType type)
            {
                if (type == null) return [];
                var baseFacets = MergeRestrictions(type.BaseXmlSchemaType as XmlSchemaSimpleType);
                if (type.Content is XmlSchemaSimpleTypeRestriction typeRestriction)
                {
                    var facets = typeRestriction.Facets.Cast<XmlSchemaFacet>().ToList();
                    foreach (var facet in facets)
                    {
                        var baseFacet = baseFacets
                            .SingleOrDefault(f => f is not XmlSchemaEnumerationFacet
                                && f.GetType() == facet.GetType());
                        if (baseFacet != null)
                            baseFacets.Remove(baseFacet);
                        baseFacets.Add(facet);
                    }
                }
                return baseFacets;
            }
        }
        
         private EnumModel CreateEnumModel(XmlSchemaSimpleType simpleType, List<XmlSchemaEnumerationFacet> enumFacets)
        {
            // we got an enum
            var name = configuration.NamingProvider.EnumTypeNameFromQualifiedName(qualifiedName, simpleType);
            if (namespaceModel != null)
                name = namespaceModel.GetUniqueTypeName(name);

            var enumModel = new EnumModel(configuration)
            {
                Name = name,
                Namespace = namespaceModel,
                XmlSchemaName = qualifiedName,
                XmlSchemaType = simpleType,
                IsAnonymous = string.IsNullOrEmpty(simpleType.QualifiedName.Name),
            };

            enumModel.Documentation.AddRange(docs);

            foreach (var facet in enumFacets.DistinctBy(f => f.Value))
            {
                var value = new EnumValueModel
                {
                    Name = configuration.NamingProvider.EnumMemberNameFromValue(enumModel.Name, facet.Value, facet),
                    Value = facet.Value
                };

                var valueDocs = DocumentationExtractor.GetDocumentation(facet);
                value.Documentation.AddRange(valueDocs);

                value.IsDeprecated = facet.Annotation?.Items.OfType<XmlSchemaAppInfo>()
                    .Any(a => Array.Exists(a.Markup, m => m.Name == "annox:annotate" && m.HasChildNodes && m.FirstChild.Name == "jl:Deprecated")) == true;

                enumModel.Values.Add(value);
            }

            enumModel.Values = EnsureEnumValuesUnique(enumModel.Values);
            if (namespaceModel != null)
                namespaceModel.Types[enumModel.Name] = enumModel;

            if (!qualifiedName.IsEmpty)
                builder.SetType(simpleType, qualifiedName, enumModel);

            return enumModel;
        }
         
        public List<EnumValueModel> EnsureEnumValuesUnique(List<EnumValueModel> enumModelValues)
        {
            var enumValueGroups = from enumValue in enumModelValues
                group enumValue by enumValue.Name;

            foreach (var g in enumValueGroups)
            {
                var i = 1;
                foreach (var t in g.Skip(1))
                    t.Name = $"{t.Name}{i++}";
            }

            return enumModelValues;
        }

        private SimpleModel CreateSimpleModel(XmlSchemaSimpleType simpleType, List<RestrictionModel> restrictions)
        {
            var simpleModelName = configuration.NamingProvider.SimpleTypeNameFromQualifiedName(qualifiedName, simpleType);
            if (namespaceModel != null)
                simpleModelName = namespaceModel.GetUniqueTypeName(simpleModelName);

            var simpleModel = new SimpleModel(configuration)
            {
                Name = simpleModelName,
                Namespace = namespaceModel,
                XmlSchemaName = qualifiedName,
                XmlSchemaType = simpleType,
                ValueType = simpleType.Datatype.GetEffectiveType(configuration, restrictions, simpleType),
            };

            simpleModel.Documentation.AddRange(docs);
            simpleModel.Restrictions.AddRange(restrictions);

            if (namespaceModel != null)
                namespaceModel.Types[simpleModel.Name] = simpleModel;

            if (!qualifiedName.IsEmpty)
                builder.SetType(simpleType, qualifiedName, simpleModel);
            return simpleModel;
        }
        
        
        
        

    }
}