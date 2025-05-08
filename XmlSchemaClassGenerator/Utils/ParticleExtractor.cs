using System;
using System.Collections.Generic;
using System.Xml.Schema;

namespace XmlSchemaClassGenerator.Utils
{
    internal static class ParticleExtractor
    {
        
        public static IEnumerable<Particle> GetElements(XmlSchemaGroupBase groupBase)
        {
            if (groupBase?.Items != null)
            {
                foreach (var item in groupBase.Items)
                {
                    foreach (var element in GetElements(item, groupBase))
                    {
                        element.MaxOccurs = Math.Max(element.MaxOccurs, groupBase.MaxOccurs);
                        element.MinOccurs = Math.Min(element.MinOccurs, groupBase.MinOccurs);
                        yield return element;
                    }
                }
            }
        }
        
        public static IEnumerable<Particle> GetElements(XmlSchemaObject item, XmlSchemaObject parent)
        {
            switch (item)
            {
                case null:
                    yield break;
                case XmlSchemaElement element:
                    yield return new Particle(element, parent); break;
                case XmlSchemaAny any:
                    yield return new Particle(any, parent); break;
                case XmlSchemaGroupRef groupRef:
                    yield return new Particle(groupRef, parent); break;
                case XmlSchemaGroupBase itemGroupBase:
                    foreach (var groupBaseElement in GetElements(itemGroupBase))
                        yield return groupBaseElement;
                    break;
            }
        }
        
       
    }
}