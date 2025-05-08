﻿using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace XmlSchemaClassGenerator.Model;

[DebuggerDisplay("{Name}")]
public abstract class TypeModel(GeneratorConfiguration configuration) : GeneratorModel(configuration)
{
    protected static readonly CodeDomProvider CSharpProvider = CodeDomProvider.CreateProvider("CSharp");

    public NamespaceModel Namespace { get; set; }
    public XmlSchemaElement RootElement { get; set; }
    public XmlQualifiedName RootElementName { get; set; }
    public bool IsAbstractRoot { get; set; }
    public string Name { get; set; }
    public XmlQualifiedName XmlSchemaName { get; set; }
    public XmlSchemaType XmlSchemaType { get; set; }
    public List<DocumentationModel> Documentation { get; } = [];
    public bool IsAnonymous { get; set; }
    public virtual bool IsSubtype => false;
    public virtual bool IsRedefined => false;
    
    /// <summary>Alle Choice-Enum-Definitionen, die für diesen Typ erzeugt wurden.</summary>
    public List<ChoiceEnumDefinition> ChoiceEnumDefinitions { get; } = new List<ChoiceEnumDefinition>();

    
    public int ChoicePropertiesCount { get; set; }
    public bool IsChoice { get; set; }
    
    public virtual CodeTypeDeclaration Generate()
    {
        var typeDeclaration = new CodeTypeDeclaration { Name = Name };

        typeDeclaration.Comments.AddRange(GetComments(Documentation).ToArray());

        AddDescription(typeDeclaration.CustomAttributes, Documentation);

        var generatedAttribute = AttributeDecl<GeneratedCodeAttribute>(
            new(new CodePrimitiveExpression(Configuration.Version.Title)),
            new(new CodePrimitiveExpression(Configuration.CreateGeneratedCodeAttributeVersion ? Configuration.Version.Version : "")));
        typeDeclaration.CustomAttributes.Add(generatedAttribute);

        return typeDeclaration;
    }

    protected void GenerateTypeAttribute(CodeTypeDeclaration typeDeclaration)
    {
        var xmlSchemaName = XmlSchemaName;

        if (xmlSchemaName == null && RootElementName != null && typeDeclaration.Name != RootElementName.Name)
            xmlSchemaName = RootElementName;

        if (xmlSchemaName == null || IsRedefined) return;

        var typeAttribute = AttributeDecl<XmlTypeAttribute>(
            new(new CodePrimitiveExpression(xmlSchemaName.Name)),
            new(nameof(XmlRootAttribute.Namespace), new CodePrimitiveExpression(xmlSchemaName.Namespace)));

        // don't generate AnonymousType if it's derived class, otherwise XmlSerializer will
        // complain with "InvalidOperationException: Cannot include anonymous type '...'"
        if (IsAnonymous && !IsSubtype)
            typeAttribute.Arguments.Add(new("AnonymousType", new CodePrimitiveExpression(true)));

        typeDeclaration.CustomAttributes.Add(typeAttribute);
    }

    protected void GenerateSerializableAttribute(CodeTypeDeclaration typeDeclaration)
    {
        if (Configuration.GenerateSerializableAttribute)
            typeDeclaration.CustomAttributes.Add(AttributeDecl<SerializableAttribute>());
    }

    public virtual CodeTypeReference GetReferenceFor(NamespaceModel referencingNamespace, bool collection = false, bool forInit = false, bool attribute = false)
    {
        string name;
        var referencingOptions = Configuration.CodeTypeReferenceOptions;
        if (referencingNamespace == Namespace)
        {
            name = Name;
            referencingOptions = CodeTypeReferenceOptions.GenericTypeParameter;
        }
        else if ((referencingNamespace ?? Namespace).IsAmbiguous)
        {
            name = $"global::{Namespace.Name}.{Name}";
            referencingOptions = CodeTypeReferenceOptions.GenericTypeParameter;
        }
        else
        {
            name = $"{Namespace.Name}.{Name}";
        }

        if (collection)
        {
            name = forInit ? SimpleModel.GetCollectionImplementationName(name, Configuration) : SimpleModel.GetCollectionDefinitionName(name, Configuration);
            referencingOptions = Configuration.CollectionType == typeof(Array)
                ? CodeTypeReferenceOptions.GenericTypeParameter
                : Configuration.CodeTypeReferenceOptions;
        }

        return new CodeTypeReference(name, referencingOptions);
    }

    public virtual CodeExpression GetDefaultValueFor(string defaultString, bool attribute)
    {
        throw new NotSupportedException(string.Format("Getting default value for {0} not supported.", defaultString));
    }
}