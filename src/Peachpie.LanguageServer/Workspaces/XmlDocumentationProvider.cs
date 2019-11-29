using Microsoft.Build.Tasks;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using System.Xml;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Peachpie.LanguageServer.Workspaces
{
    /// <summary>
    /// A class used to provide XML documentation to the compiler for members from metadata from an XML document source.
    /// </summary>
    public abstract class XmlDocumentationProvider : DocumentationProvider
    {
        private Dictionary<string, string> _docComments;

        /// <summary>
        /// Gets the source stream for the XML document.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        protected abstract Stream GetSourceStream(CancellationToken cancellationToken);

        ///// <summary>
        ///// Creates an <see cref="XmlDocumentationProvider"/> from bytes representing XML documentation data.
        ///// </summary>
        ///// <param name="xmlDocCommentBytes">The XML document bytes.</param>
        ///// <returns>An <see cref="XmlDocumentationProvider"/>.</returns>
        //public static XmlDocumentationProvider CreateFromBytes(byte[] xmlDocCommentBytes)
        //{
        //    return new ContentBasedXmlDocumentationProvider(xmlDocCommentBytes);
        //}

        private static XmlDocumentationProvider DefaultXmlDocumentationProvider { get; } = new NullXmlDocumentationProvider();

        /// <summary>
        /// Creates an <see cref="XmlDocumentationProvider"/> from an XML documentation file.
        /// </summary>
        /// <param name="xmlDocCommentFilePath">The path to the XML file.</param>
        /// <returns>An <see cref="XmlDocumentationProvider"/>.</returns>
        public static XmlDocumentationProvider CreateFromFile(string xmlDocCommentFilePath)
        {
            if (!File.Exists(xmlDocCommentFilePath))
            {
                return DefaultXmlDocumentationProvider;
            }

            return new FileBasedXmlDocumentationProvider(xmlDocCommentFilePath);
        }

        private XDocument GetXDocument(CancellationToken cancellationToken)
        {
            using var stream = GetSourceStream(cancellationToken);
            using var xmlReader = XmlReader.Create(stream, s_xmlSettings);
            return XDocument.Load(xmlReader);
        }

        protected override string GetDocumentationForSymbol(string documentationMemberID, CultureInfo preferredCulture, CancellationToken cancellationToken = default)
        {
            if (_docComments == null)
            {
                lock (this)
                {
                    if (_docComments == null)
                    {
                        _docComments = new Dictionary<string, string>();

                        try
                        {
                            //var doc = GetXDocument(cancellationToken);
                            //foreach (var e in doc.Descendants("member"))
                            //{
                            //    if (e.Attribute("name") != null)
                            //    {
                            //        using var reader = e.CreateReader();
                            //        reader.MoveToContent();
                            //        _docComments[e.Attribute("name").Value] = reader.ReadInnerXml();
                            //    }
                            //}

                            // just cut the <member> elements from XML (there are invalid tags in netstandard.xml so we don't parse XML here) // how do they do it in other IDEs????
                            var xml = new StreamReader(GetSourceStream(cancellationToken)).ReadToEnd();
                            int from = 0;
                            for (; ; )
                            {
                                from = xml.IndexOf("<member ", from);
                                if (from < 0) break;

                                var to = xml.IndexOf("</member>", from);
                                if (to < 0) break;

                                var match = new Regex("name=\"([^\\\"]+)\"[^>]*>", RegexOptions.CultureInvariant).Match(xml, from, to - from);
                                if (match.Success)
                                {
                                    var name = match.Groups[1].Value;
                                    from = match.Index + match.Length;  // end of <member ...> element
                                    _docComments[name] = xml.Substring(from, to - from); // inner xml
                                }

                                //
                                from = to;
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }

            return _docComments.TryGetValue(documentationMemberID, out var docComment) ? docComment : string.Empty;
        }

        private static readonly XmlReaderSettings s_xmlSettings = new XmlReaderSettings()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            CheckCharacters = false,
            IgnoreComments = true,
            ValidationFlags = System.Xml.Schema.XmlSchemaValidationFlags.None,
            IgnoreWhitespace = true,
            IgnoreProcessingInstructions = true,
            ConformanceLevel = ConformanceLevel.Auto,
            CloseInput = true,
        };

        //private sealed class ContentBasedXmlDocumentationProvider : XmlDocumentationProvider
        //{
        //    private readonly byte[] _xmlDocCommentBytes;

        //    public ContentBasedXmlDocumentationProvider(byte[] xmlDocCommentBytes)
        //    {
        //        _xmlDocCommentBytes = xmlDocCommentBytes;
        //    }

        //    protected override Stream GetSourceStream(CancellationToken cancellationToken)
        //    {
        //        return new MemoryStream(_xmlDocCommentBytes, false);
        //    }

        //    public override bool Equals(object obj)
        //    {
        //        var other = obj as ContentBasedXmlDocumentationProvider;
        //        return other != null && EqualsHelper(other);
        //    }

        //    private bool EqualsHelper(ContentBasedXmlDocumentationProvider other)
        //    {
        //        // Check for reference equality first
        //        if (this == other || _xmlDocCommentBytes == other._xmlDocCommentBytes)
        //        {
        //            return true;
        //        }

        //        // Compare byte sequences
        //        if (_xmlDocCommentBytes.Length != other._xmlDocCommentBytes.Length)
        //        {
        //            return false;
        //        }

        //        for (var i = 0; i < _xmlDocCommentBytes.Length; i++)
        //        {
        //            if (_xmlDocCommentBytes[i] != other._xmlDocCommentBytes[i])
        //            {
        //                return false;
        //            }
        //        }

        //        return true;
        //    }

        //    public override int GetHashCode()
        //    {
        //        return Hash.CombineValues(_xmlDocCommentBytes);
        //    }
        //}

        private sealed class FileBasedXmlDocumentationProvider : XmlDocumentationProvider
        {
            private readonly string _filePath;

            public FileBasedXmlDocumentationProvider(string filePath)
            {
                _filePath = filePath;
            }

            protected override Stream GetSourceStream(CancellationToken cancellationToken)
            {
                return new FileStream(_filePath, FileMode.Open, FileAccess.Read);
            }

            public override bool Equals(object obj)
            {
                var other = obj as FileBasedXmlDocumentationProvider;
                return other != null && _filePath == other._filePath;
            }

            public override int GetHashCode()
            {
                return _filePath.GetHashCode();
            }
        }

        /// <summary>
        /// A trivial XmlDocumentationProvider which never returns documentation.
        /// </summary>
        private sealed class NullXmlDocumentationProvider : XmlDocumentationProvider
        {
            protected override string GetDocumentationForSymbol(string documentationMemberID, CultureInfo preferredCulture, CancellationToken cancellationToken = default)
            {
                return "";
            }

            protected override Stream GetSourceStream(CancellationToken cancellationToken)
            {
                return new MemoryStream();
            }

            public override bool Equals(object obj)
            {
                // Only one instance is expected to exist, so reference equality is fine.
                return (object)this == obj;
            }

            public override int GetHashCode()
            {
                return 0;
            }
        }
    }
}
