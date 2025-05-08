using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using XmlSchemaClassGenerator.Model;
using XmlSchemaClassGenerator.Utils;

namespace XmlSchemaClassGenerator.Builder
{
    internal static class PropertyFactory
    {
        public static IEnumerable<PropertyModel> CreatePropertiesForAttributes(
            ModelBuilder builder,
            GeneratorConfiguration configuration,
            Uri source, TypeModel owningTypeModel, IEnumerable<XmlSchemaObject> items)
        {
            var properties = new List<PropertyModel>();

            foreach (var item in items)
            {
                switch (item)
                {
                    case XmlSchemaAttribute attribute when attribute.Use != XmlSchemaUse.Prohibited:

                        properties.Add(CreatePropertyFromAttribute(builder, configuration, owningTypeModel, attribute,
                            properties));
                        break;

                    case XmlSchemaAttributeGroupRef attributeGroupRef:

                        foreach (var attributeGroup in builder.AttributeGroups[attributeGroupRef.RefName])
                        {
                            if (configuration.GenerateInterfaces)
                                builder.CreateTypeModel(attributeGroupRef.RefName, attributeGroup);

                            var attributes = attributeGroup.Attributes.Cast<XmlSchemaObject>()
                                .Where(a => !(a is XmlSchemaAttributeGroupRef agr &&
                                              agr.RefName == attributeGroupRef.RefName))
                                .ToList();

                            if (attributeGroup.RedefinedAttributeGroup != null)
                            {
                                var attrs = attributeGroup.RedefinedAttributeGroup.Attributes.Cast<XmlSchemaObject>()
                                    .Where(a => !(a is XmlSchemaAttributeGroupRef agr &&
                                                  agr.RefName == attributeGroupRef.RefName)).ToList();

                                foreach (var attr in attrs)
                                {
                                    var n = attr.GetQualifiedName();

                                    if (n != null)
                                        attributes.RemoveAll(a => a.GetQualifiedName() == n);

                                    attributes.Add(attr);
                                }
                            }

                            var newProperties = CreatePropertiesForAttributes(builder, configuration, source,
                                owningTypeModel, attributes);
                            properties.AddRange(newProperties);
                        }

                        break;
                }
            }

            return properties;
        }

        static PropertyModel CreatePropertyFromAttribute(
            ModelBuilder builder,
            GeneratorConfiguration configuration,
            TypeModel owningTypeModel, XmlSchemaAttributeEx attribute,
            IList<PropertyModel> properties)
        {
            var attributeQualifiedName = attribute.AttributeSchemaType.QualifiedName;
            var name = configuration.NamingProvider.AttributeNameFromQualifiedName(attribute.QualifiedName, attribute);
            var originalName = name;

            if (attribute.Base.Parent is XmlSchemaAttributeGroup attributeGroup
                && attributeGroup.QualifiedName != owningTypeModel.XmlSchemaName
                && builder.Types.TryGetValue(ModelBuilder.BuildKey(attributeGroup, attributeGroup.QualifiedName),
                    out var typeModelValue)
                && typeModelValue is InterfaceModel interfaceTypeModel)
            {
                var interfaceProperty =
                    interfaceTypeModel.Properties.Single(p => p.XmlSchemaName == attribute.QualifiedName);
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
                        var typeName = configuration.NamingProvider.TypeNameFromAttribute(owningTypeModel.Name,
                            attribute.QualifiedName.Name, attribute);
                        attributeQualifiedName =
                            new XmlQualifiedName(typeName, owningTypeModel.XmlSchemaName.Namespace);
                        // try to avoid name clashes
                        if (QualifiedNameResolver.NameExists(builder._set, attributeQualifiedName))
                            attributeQualifiedName = new[]
                                    { Constants.ItemName, Constants.PropertyName, Constants.ElementName }
                                .Select(s => new XmlQualifiedName(attributeQualifiedName.Name + s,
                                    attributeQualifiedName.Namespace)).First(n =>
                                    !QualifiedNameResolver.NameExists(builder._set, n));
                    }
                }

                if (name == owningTypeModel.Name)
                    name += Constants.PropertyName;
            }

            name = owningTypeModel.GetUniquePropertyName(name, properties);

            var typeModel = builder.CreateTypeModel(attributeQualifiedName, attribute.AttributeSchemaType);
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

        /// <summary>
        /// Generates PropertyModel instances for the given element particles,
        /// handling elements, wildcards (<any>), and group references.
        /// Duplicates are removed at the end.
        /// </summary>
        internal static IEnumerable<PropertyModel> CreatePropertiesForElements(
            ModelBuilder builder,
            GeneratorConfiguration configuration,
            Uri source, TypeModel owningTypeModel, Particle particle, IEnumerable<Particle> items,
            Substitute substitute = null, int order = 0, bool passProperties = true)
        {
            var properties = new List<PropertyModel>();

            #region Element & Any & GroupRef processing

            foreach (var item in items)
            {
                PropertyModel property = null;


                // --- Choice handling example: generate single Item + Enum for xs:choice ---
                if (item.XmlParent is XmlSchemaChoice choice)
                {
                    // collect all particles under this same choice
                    var choiceItems = items.Where(i => i.XmlParent == choice).ToList();
                    if (choiceItems.Count > 0)
                    {
                        
                        // property = CreatePropertyFromElement(builder, configuration, owningTypeModel,  ,
                        //     particle, item, substitute, passProperties ? properties : []);
                        //
                        //
                        // // reuse existing order counter or start at 0
                        // var props =  CreatePropertiesForChoice(builder, configuration, source, owningTypeModel, choice, choiceItems, ref order);
                        //
                        //
                        //
                        // properties.AddRange(props);
                        // ChoiceHandler.ProcessChoice(
                        //     builder,
                        //     configuration,
                        //     source,
                        //     owningTypeModel,
                        //     choice,
                        //     choiceItems,
                        //     ref localOrder,
                        //     properties);
                        // // exit early: skip default per-item creation
                        // return properties;
                    }
                }
            //    else
                {
                    switch (item.XmlParticle)
                    {
                        #region Element case

                        case XmlSchemaElement element when element.ElementSchemaType != null:
                            property = CreatePropertyFromElement(builder, configuration, owningTypeModel, element,
                                particle, item,
                                substitute, passProperties ? properties : []);
                            break;

                        #endregion

                        #region Any case

                        case XmlSchemaAny:
                            SimpleModel typeModel = new(configuration)
                            {
                                ValueType = configuration.UseXElementForAny ? typeof(XElement) : typeof(XmlElement),
                                UseDataTypeAttribute = false
                            };
                            property = new PropertyModel(configuration, "Any", typeModel, owningTypeModel)
                                { IsAny = true };
                            property.SetFromParticles(particle, item, item.MinOccurs >= 1.0m && !
                                NullabilityHelper.IsNullableByChoice(item.XmlParent));
                            break;

                        #endregion

                        #region GroupRef case

                        case XmlSchemaGroupRef groupRef:
                            foreach (var p in ProcessGroupRef(builder, configuration, source, owningTypeModel, particle,
                                         groupRef, ref order))
                                properties.Add(p);
                            break;

                        #endregion
                    }
                }

                #region Post-processing: docs, order, deprecated, dedupe

                // Discard duplicate property names. This is most likely due to:
                // - Choice or
                // - Element and attribute with the same name
                if (property != null && !properties.Exists(p => p.Name == property.Name))
                {
                    var itemDocs = DocumentationExtractor.GetDocumentation(item.XmlParticle);
                    property.Documentation.AddRange(itemDocs);

                    if (configuration.EmitOrder)
                        property.Order = order++;

                    property.IsDeprecated = itemDocs.Exists(d => d.Text.StartsWith("DEPRECATED"));

                    properties.Add(property);
                }

                #endregion
            }

            #endregion

            return properties;
        }

        /// <summary>
        /// Erzeugt für ein xs:choice ein einziges „Item“-Property und die Enum-Identifier-Property.
        /// </summary>
        private static IEnumerable<PropertyModel> CreatePropertiesForChoice(
            ModelBuilder builder,
            GeneratorConfiguration configuration,
            Uri source,
            TypeModel owningTypeModel,
            XmlSchemaChoice choice,
            List<Particle> choiceItems,
            ref int order)
        {
            var props = new List<PropertyModel>();

            // 1) Enum-Werte anlegen
            var enumValues = choiceItems
                .Select(i => ((XmlSchemaElementEx)i.XmlParticle).QualifiedName)
                .Select(qn => new EnumValueModel(qn.Name, qn))
                .ToList();
            var enumName = $"{owningTypeModel.Name}ChoiceItemType{owningTypeModel.ChoicePropertiesCount + 1}";
            owningTypeModel.ChoicePropertiesCount++;
            owningTypeModel.ChoiceEnumDefinitions
                .Add(new ChoiceEnumDefinition(enumName, enumValues));

            // 2) Item-Property
            var itemModel = new SimpleModel(configuration)  { ValueType = typeof(object) };
            var itemProp = new PropertyModel(configuration, "Item", itemModel, owningTypeModel)
            {
                IsChoice = true,
                ChoiceEnumName = enumName,
                ChoiceElementTypes = choiceItems.Select(i => new ChoiceElementInfo
                {
                    ElementName = ((XmlSchemaElementEx)i.XmlParticle).QualifiedName,
                    ElementType = builder.CreateTypeModel(
                        ((XmlSchemaElementEx)i.XmlParticle).QualifiedName,
                        ((XmlSchemaElementEx)i.XmlParticle).ElementSchemaType),
                    IsCollection = i.MaxOccurs > 1 || i.MaxOccurs == decimal.MaxValue
                }).ToList(),
                ChoiceIdentifierFieldName = $"{enumName}Value"
            };
            if (configuration.EmitOrder)
                itemProp.Order = order++;
            props.Add(itemProp);

            // 3) Enum-Identifier-Property
            var enumPropModel = new SimpleModel(configuration) /* oder spezielles EnumModel */;
            var enumProp = new PropertyModel(
                configuration,
                $"{enumName}Value",
                new EnumModel(configuration)
                {
                    Name = enumName,
                    // Source = source
                } ,  //enumName, source),
                owningTypeModel)
            {
                IsChoiceIdentifier = true
            };
            if (configuration.EmitOrder)
                enumProp.Order = order++;
            props.Add(enumProp);

            return props;
        }

        private static IEnumerable<PropertyModel> ProcessGroupRef(
            ModelBuilder builder,
            GeneratorConfiguration configuration,
            Uri source,
            TypeModel owningTypeModel,
            Particle parentParticle,
            XmlSchemaGroupRef groupRef,
            ref int order)
        {
            var props = new List<PropertyModel>();
            var groups = builder.Groups[groupRef.RefName];

            if (configuration.GenerateInterfaces)
                builder.CreateTypeModel(groupRef.RefName, groups.First());

            var groupItems = ParticleExtractor.GetElements(groupRef.Particle).ToList();
            foreach (var item in groupItems)
            {
                var nestedProps = CreatePropertiesForElements(
                    builder, configuration, source,
                    owningTypeModel, parentParticle, new[] { item },
                    substitute: null, order: order, passProperties: true);

                if (configuration.EmitOrder)
                    order += nestedProps.Count();

                props.AddRange(nestedProps);
            }

            return props;
        }

        private static PropertyModel CreatePropertyFromElement(
            ModelBuilder builder,
            GeneratorConfiguration configuration,
            TypeModel owningTypeModel, XmlSchemaElementEx element, Particle particle, Particle item,
            Substitute substitute,
            IList<PropertyModel> properties)
        {
            PropertyModel property;
            XmlSchemaElementEx effectiveElement = substitute?.Element ?? element;

            property = properties.FirstOrDefault(p =>
                element.QualifiedName == p.XmlSchemaName && p.Type.XmlSchemaType == element.ElementSchemaType);

            if (property != null)
            {
                property.IsCollection = true;
                return property;
            }

            var name = configuration.NamingProvider.ElementNameFromQualifiedName(effectiveElement.QualifiedName,
                effectiveElement);
            var originalName = name;
            if (name == owningTypeModel.Name)
                name += Constants.PropertyName; // member names cannot be the same as their enclosing type

            name = owningTypeModel.GetUniquePropertyName(name, properties);

            var typeModel = substitute?.Type ?? builder.CreateTypeModel(
                QualifiedNameResolver.GetQualifiedName(builder, configuration, owningTypeModel, particle.XmlParticle,
                    element), element.ElementSchemaType);

            property = new PropertyModel(configuration, name, typeModel, owningTypeModel)
                { IsNillable = element.IsNillable };
            var isRequired = item.MinOccurs >= 1.0m && !NullabilityHelper.IsNullableByChoice(item.XmlParent);
            property.SetFromParticles(particle, item, isRequired);
            property.SetFromNode(originalName, isRequired, element);
            property.SetSchemaNameAndNamespace(owningTypeModel, effectiveElement);

            if (property.IsArray && !configuration.GenerateComplexTypesForCollections)
                property.Type.Namespace.Types.Remove(property.Type.Name);

            return property;
        }
    }
}