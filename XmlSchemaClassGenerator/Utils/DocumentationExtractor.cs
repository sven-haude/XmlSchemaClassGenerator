using System.Collections.Generic;
using System.Linq;
using System.Xml.Schema;
using XmlSchemaClassGenerator.Model;

namespace XmlSchemaClassGenerator.Utils
{
    internal static class DocumentationExtractor
    {
        public static List<DocumentationModel> GetDocumentation(XmlSchemaAnnotated annotated)
        {
            return annotated.Annotation == null ? []
                : annotated.Annotation.Items.OfType<XmlSchemaDocumentation>()
                    .Where(d => d.Markup?.Length > 0)
                    .Select(d => d.Markup.Select(m => new DocumentationModel { Language = d.Language, Text = m.OuterXml }))
                    .SelectMany(d => d)
                    .Where(d => !string.IsNullOrEmpty(d.Text))
                    .ToList();
        }
    }
}