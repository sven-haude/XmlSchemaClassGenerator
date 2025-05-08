using System.Xml.Schema;

namespace XmlSchemaClassGenerator.Utils
{
    internal static class NullabilityHelper
    {
        internal static bool IsNullableByChoice(XmlSchemaObject parent)
        {
            while (parent != null)
            {
                switch (parent)
                {
                    case XmlSchemaChoice:
                        return true;
                    // Any ancestor element between the current item and the
                    // choice would already have been forced to nullable.
                    case XmlSchemaElement:
                    case XmlSchemaParticle p when p.MinOccurs < 1.0m:
                        return false;
                    default:
                        break;
                }
                parent = parent.Parent;
            }
            return false;
        }
    }
}