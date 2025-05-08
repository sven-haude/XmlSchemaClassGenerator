using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;

namespace XmlSchemaClassGenerator.Services
{
    /// <summary>
    /// Lädt eine Menge von XSD-Quellen in ein kompiliertes <see cref="XmlSchemaSet"/>.
    /// </summary>
    public interface ISchemaLoader
    {
        XmlSchemaSet Load(
            IEnumerable<XmlReader> readers,
            GeneratorConfiguration configuration,
            Action<string>? log,
            out bool validationError);
    }
}