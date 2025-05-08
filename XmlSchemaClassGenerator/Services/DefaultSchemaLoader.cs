#nullable enable
using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;

namespace XmlSchemaClassGenerator.Services
{
    internal sealed class DefaultSchemaLoader : ISchemaLoader
    {
        public XmlSchemaSet Load(
            IEnumerable<XmlReader> readers,
            GeneratorConfiguration cfg,
            Action<string>? log,
            out bool validationError)
        {
            #nullable enable
            bool hasError = false;

            var set = new XmlSchemaSet
            {
                XmlResolver = new NormalizingXmlResolver(cfg.ForceUriScheme)
            };
            set.CompilationSettings.EnableUpaCheck = cfg.EnableUpaCheck;

            set.ValidationEventHandler += (_, e) =>
            {
                var ex = e.Exception as XmlSchemaException;
                while (ex != null)
                {
                    hasError = true;
                    log?.Invoke(ex.Message);
                    ex = ex.InnerException as XmlSchemaException;
                }
            };

            foreach (var rdr in readers)
                set.Add(null, rdr);

            set.Compile();
            validationError = hasError;
            return set;
        }
    }
}