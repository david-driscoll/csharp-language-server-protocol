using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Statiq.CodeAnalysis;
using Statiq.Common;
using Statiq.Html;

namespace Generator
{
    /// <summary>
    /// Extensions used by the views.
    /// </summary>
    public static class Extensions
    {
        public static HtmlString Name(this IDocument document) => new HtmlString(FormatName(document.GetString(CodeAnalysisKeys.DisplayName)));

        public static HtmlString GetTypeLink(this IExecutionContext context, IDocument document) => context.GetTypeLink(document, null, true);

        public static HtmlString GetTypeLink(this IExecutionContext context, IDocument document, bool linkTypeArguments) => context.GetTypeLink(document, null, linkTypeArguments);

        public static HtmlString GetTypeLink(this IExecutionContext context, IDocument document, string name) => context.GetTypeLink(document, name, true);

        public static HtmlString GetTypeLink(this IExecutionContext context, IDocument document, string name, bool linkTypeArguments)
        {
            name = name ?? document.GetString(CodeAnalysisKeys.DisplayName);

            // Link nullable types to their type argument
            if (document.GetString("Name") == "Nullable")
            {
                IDocument nullableType = document.GetDocumentList(CodeAnalysisKeys.TypeArguments)?.FirstOrDefault();
                if (nullableType != null)
                {
                    return context.GetTypeLink(nullableType, name);
                }
            }

            // If it wasn't nullable, format the name
            name = FormatName(name);

            // Link the type and type parameters seperatly for generic types
            IReadOnlyList<IDocument> typeArguments = document.GetDocumentList(CodeAnalysisKeys.TypeArguments);
            if (typeArguments?.Count > 0)
            {
                // Link to the original definition of the generic type
                document = document.GetDocument(CodeAnalysisKeys.OriginalDefinition) ?? document;

                if (linkTypeArguments)
                {
                    // Get the type argument positions
                    int begin = name.IndexOf("<wbr>&lt;") + 9;
                    int openParen = name.IndexOf("&gt;<wbr>(");
                    int end = name.LastIndexOf("&gt;<wbr>", openParen == -1 ? name.Length : openParen);  // Don't look past the opening paren if there is one

                    // Remove existing type arguments and insert linked type arguments (do this first to preserve original indexes)
                    if (end - begin > 0)
                    {
                        name = name
                            .Remove(begin, end - begin)
                            .Insert(begin, string.Join(", <wbr>", typeArguments.Select(x => context.GetTypeLink(x, true).Value)));

                        // Insert the link for the type
                        if (!document.Destination.IsNull)
                        {
                            name = name.Insert(begin - 9, "</a>").Insert(0, $"<a href=\"{context.GetLink(document.Destination)}\">");
                        }
                    }

                    return new HtmlString(name);
                }
            }

            // If it's a type parameter, create an anchor link to the declaring type's original definition
            if (document.GetString("Kind") == "TypeParameter")
            {
                IDocument declaringType = document.GetDocument(CodeAnalysisKeys.DeclaringType)?.GetDocument(CodeAnalysisKeys.OriginalDefinition);
                if (declaringType != null)
                {
                    return new HtmlString(declaringType.Destination.IsNull
                        ? name
                        : $"<a href=\"{context.GetLink(declaringType.Destination)}#typeparam-{document["Name"]}\">{name}</a>");
                }
            }

            return new HtmlString(document.Destination.IsNull
                ? name
                : $"<a href=\"{context.GetLink(document.Destination)}\">{name}</a>");
        }

        // https://stackoverflow.com/a/3143036/807064
        private static IEnumerable<string> SplitAndKeep(this string s, params char[] delims)
        {
            int start = 0, index;

            while ((index = s.IndexOfAny(delims, start)) != -1)
            {
                if (index - start > 0)
                {
                    yield return s.Substring(start, index - start);
                }

                yield return s.Substring(index, 1);
                start = index + 1;
            }

            if (start < s.Length)
            {
                yield return s.Substring(start);
            }
        }

        private static string FormatName(string name)
        {
            if (name == null)
            {
                return string.Empty;
            }

            // Encode and replace .()<> with word break opportunities
            name = WebUtility.HtmlEncode(name)
                .Replace(".", "<wbr>.")
                .Replace("(", "<wbr>(")
                .Replace(")", ")<wbr>")
                .Replace(", ", ", <wbr>")
                .Replace("&lt;", "<wbr>&lt;")
                .Replace("&gt;", "&gt;<wbr>");

            // Add additional break opportunities in long un-broken segments
            List<string> segments = name.Split(new[] { "<wbr>" }, StringSplitOptions.None).ToList();
            bool replaced = false;
            for (int c = 0; c < segments.Count; c++)
            {
                if (segments[c].Length > 20)
                {
                    segments[c] = new string(segments[c]
                        .SelectMany((x, i) => char.IsUpper(x) && i != 0 ? new[] { '<', 'w', 'b', 'r', '>', x } : new[] { x })
                        .ToArray());
                    replaced = true;
                }
            }

            return replaced ? string.Join("<wbr>", segments) : name;
        }

        /// <summary>
        /// Generates links to each heading on a page and returns a string containing all of the links.
        /// </summary>
        public static async Task<string> GenerateInfobarHeadingsAsync(this IExecutionContext context, IDocument document)
        {
            StringBuilder content = new StringBuilder();
            IReadOnlyList<IDocument> headings = document.GetDocumentList(HtmlKeys.Headings);
            if (headings != null)
            {
                foreach (IDocument heading in headings)
                {
                    string id = heading.GetString(HtmlKeys.HeadingId);
                    if (id != null)
                    {
                        content.AppendLine($"<p><a href=\"#{id}\">{await heading.GetContentStringAsync()}</a></p>");
                    }
                }
            }
            if (content.Length > 0)
            {
                content.Insert(0, "<h6>On This Page</h6>");
                content.AppendLine("<hr class=\"infobar-hidden\" />");
                return content.ToString();
            }
            return null;
        }
    }
}