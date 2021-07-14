using Statiq.Common;
using System.Collections.Generic;

namespace Generator
{
    /// <summary>
    /// This model is used for the DocsTable partial that renders documents, titles, headers, and summaries as a table.
    /// </summary>
    public class DocsTable
    {
        public IList<IDocument> Docs { get; set; } = new List<IDocument>();
        public string Title { get; set; } = string.Empty;
        public string Header { get; set; } = string.Empty;
        public bool HasSummary { get; set; }
        public bool LinkTypeArguments { get; set; } = true;
    }
}
