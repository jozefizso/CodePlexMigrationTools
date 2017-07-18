using System;
using System.IO;
using System.Text;
using System.Xml;

namespace CodeplexMigration.IssueMigrator.Codeplex
{
    /// <summary>
    /// Special XmlTextWriter that preserves spaces in elements that contains HTML data.
    /// The writer detects elements with names ending with "Html" suffix.
    /// It will add the xml:space="preserve" attribute to those elements
    /// about it will output string content as CDATA section.
    /// </summary>
    public class CodeplexXmlWriter : XmlTextWriter
    {
        private bool isInHtmlElementContext;

        public CodeplexXmlWriter(Stream w, Encoding encoding) : base(w, encoding)
        {
        }

        public CodeplexXmlWriter(string filename, Encoding encoding) : base(filename, encoding)
        {
        }

        public CodeplexXmlWriter(TextWriter w) : base(w)
        {
        }

        public override void WriteStartElement(string prefix, string localName, string ns)
        {
            base.WriteStartElement(prefix, localName, ns);

            if (localName.EndsWith("Html"))
            {
                base.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                this.isInHtmlElementContext = true;
            }
        }

        public override void WriteEndElement()
        {
            this.isInHtmlElementContext = false;
            base.WriteEndElement();
        }

        public override void WriteString(string text)
        {
            if (this.isInHtmlElementContext)
            {
                base.WriteCData(text);
            }
            else
            {
                base.WriteString(text);
            }
        }
    }
}
