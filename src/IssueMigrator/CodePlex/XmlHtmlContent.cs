using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace IssueMigrator.CodePlex
{
    public class XmlHtmlContent
    {
        [XmlAttribute("space", Namespace = "http://www.w3.org/XML/1998/namespace")]
        public string Space = "preserve";

        [XmlElement("")]
        public XmlCDataSection CDataContent
        {
            get
            {
                return new XmlDocument().CreateCDataSection(this.Content);
            }
            set
            {
                this.Content = value.Value;
            }
        }

        [XmlText]
        public string Content;
    }
}
