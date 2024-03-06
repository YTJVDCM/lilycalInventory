using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.UIElements.Cursor;
#if UNITY_2022_3_OR_NEWER
using UnityEngine.TextCore.Text;
#endif

namespace jp.lilxyzw.lilycalinventory
{
    internal static class MarkdownViewer
    {
        private static Dictionary<string,List<(MDType,string,int,int)>> mdlist = new Dictionary<string,List<(MDType,string,int,int)>>();
        private static Encoding encSjis = Encoding.GetEncoding("Shift_JIS");

        internal enum MDType
        {
            p,
            h1,
            h2,
            h3,
            h4,
            h5,
            h6,
            ul,
            ol,
            h1line,
            h2line,
            br
        }

        #if UNITY_2022_3_OR_NEWER
        internal class MDLabel : Label
        {
            private const float FONT_SIZE = 14f;
            private static Dictionary<MouseCursor, Cursor> cursors = new Dictionary<MouseCursor, Cursor>();

            private static readonly string[] fontNamesEn = {"Inter", "Arial"};
            private static readonly string[] fontNamesJp = {"Yu Gothic UI", "Meiryo UI"};
            private static bool isInitialized = false;
            private static FontAsset m_FontAsset = null;
            private static FontAsset fontAsset => m_FontAsset ? m_FontAsset : m_FontAsset = InitializeFontAsset();
            private static FontDefinition fontDefinition = FontDefinition.FromSDFFont(fontAsset);

            private static FontAsset InitializeFontAsset()
            {
                if(isInitialized) return m_FontAsset;
                isInitialized = true;
                var allFonts = Font.GetOSInstalledFontNames();

                foreach(var fontName in fontNamesEn)
                    if(allFonts.Contains(fontName)) 
                        AddFont(FontAsset.CreateFontAsset(fontName, ""));

                foreach(var fontName in fontNamesJp)
                    if(allFonts.Contains(fontName)) 
                        AddFont(FontAsset.CreateFontAsset(fontName, ""));

                return m_FontAsset;
            }

            private static void AddFont(FontAsset fontAsset)
            {
                if(m_FontAsset)
                {
                    m_FontAsset.fallbackFontAssetTable.Add(fontAsset);
                    return;
                }

                m_FontAsset = fontAsset;
                m_FontAsset.fallbackFontAssetTable = new List<FontAsset>();
            }

            private static void SetFont(IStyle style)
            {
                if(fontAsset) style.unityFontDefinition = fontDefinition;
            }

            private static Cursor GetDefaultCursor(MouseCursor mouseCursor)
            {
                if(cursors.ContainsKey(mouseCursor)) return cursors[mouseCursor];
                object cursor = new Cursor();
                typeof(Cursor).GetProperty("defaultCursorId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cursor, (int)mouseCursor);
                return cursors[mouseCursor] = (Cursor)cursor;
            }

            internal MDLabel(string label, MDType type, bool enableSpace)
            {
                SetFont(style);
                text = label;
                enableRichText = true;
                focusable = true;
                selection.isSelectable = true;
                style.cursor = GetDefaultCursor(MouseCursor.Text);

                style.whiteSpace = WhiteSpace.Normal;
                if(enableSpace) style.marginTop = 12;
                switch(type)
                {
                    case MDType.h1:
                        style.fontSize = FONT_SIZE * 2.0f;
                        style.unityFontStyleAndWeight = FontStyle.Bold;
                        style.borderBottomColor = new Color(0.5f,0.5f,0.5f,0.5f);
                        style.borderBottomWidth = 1;
                        break;
                    case MDType.h2:
                        style.fontSize = FONT_SIZE * 1.5f;
                        style.unityFontStyleAndWeight = FontStyle.Bold;
                        style.borderBottomColor = new Color(0.5f,0.5f,0.5f,0.5f);
                        style.borderBottomWidth = 1;
                        break;
                    case MDType.h3:
                        style.fontSize = FONT_SIZE * 1.25f;
                        style.unityFontStyleAndWeight = FontStyle.Bold;
                        break;
                    case MDType.h4:
                    case MDType.h5:
                    case MDType.h6:
                        style.fontSize = FONT_SIZE;
                        style.unityFontStyleAndWeight = FontStyle.Bold;
                        break;
                    default:
                        style.fontSize = FONT_SIZE;
                        break;
                }
            }

            internal MDLabel(string label)
            {
                SetFont(style);
                text = label;
                enableRichText = true;
                style.fontSize = FONT_SIZE;
            }
        }
        #else
        private static PropertyInfo hyperlinkInfos;
        private static bool isInitializedReflection = false;

        [InitializeOnLoadMethod]
        private static void AddLILHref()
        {
            try
            {
                var evt = typeof(EditorGUI).GetEvent("hyperLinkClicked", BindingFlags.Static | BindingFlags.NonPublic);
                var method = typeof(MarkdownViewer).GetMethod("OpenLink", BindingFlags.Static | BindingFlags.NonPublic);
                var del = System.Delegate.CreateDelegate(evt.EventHandlerType, method);
                evt.AddMethod.Invoke(null, new object[]{del});
            }
            catch(System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void OpenLink(object sender, System.EventArgs args)
        {
            if(!isInitializedReflection) hyperlinkInfos = args.GetType().GetProperty("hyperlinkInfos", BindingFlags.Instance | BindingFlags.Public);
            if(hyperlinkInfos == null) return;
            if(hyperlinkInfos.GetValue(args) is Dictionary<string, string> hyperLinkData)
                Application.OpenURL(hyperLinkData["lilhref"]);
        }

        internal class MDLabel : Label
        {
            private const float FONT_SIZE = 14f;

            internal MDLabel(string label, MDType type, bool enableSpace)
            {
                var style = new GUIStyle(EditorStyles.label);
                style.SetColors(style.normal.textColor);
                style.richText = true;
                style.fontSize = (int)FONT_SIZE;
                style.wordWrap = true;
                RemoveHorizontalSpace(style);

                switch(type)
                {
                    case MDType.h1:
                        style.fontSize = (int)(FONT_SIZE * 2.0f);
                        style.fontStyle = FontStyle.Bold;
                        break;
                    case MDType.h2:
                        style.fontSize = (int)(FONT_SIZE * 1.5f);
                        style.fontStyle = FontStyle.Bold;
                        break;
                    case MDType.h3:
                        style.fontSize = (int)(FONT_SIZE * 1.25f);
                        style.fontStyle = FontStyle.Bold;
                        break;
                    case MDType.h4:
                    case MDType.h5:
                    case MDType.h6:
                        style.fontSize = (int)FONT_SIZE;
                        style.fontStyle = FontStyle.Bold;
                        break;
                    default:
                        style.fontSize = (int)FONT_SIZE;
                        break;
                }
                var container = new IMGUIContainer(() => {
                    if(enableSpace) EditorGUILayout.Space(12);
                    EditorGUILayout.SelectableLabel(label, style, GUILayout.Height(style.CalcHeight(new GUIContent(label), resolvedStyle.width - 3)));
                });
                if(type == MDType.h1 || type == MDType.h2)
                {
                    container.style.borderBottomColor = new Color(0.5f,0.5f,0.5f,0.5f);
                    container.style.borderBottomWidth = 1;
                }
                Add(container);
            }

            internal MDLabel(string label)
            {
                var style = new GUIStyle(EditorStyles.label);
                style.SetColors(style.normal.textColor);
                style.richText = true;
                style.fontSize = (int)FONT_SIZE;
                RemoveHorizontalSpace(style);
                style.alignment = TextAnchor.UpperRight;
                var layoutHeight = GUILayout.Height(FONT_SIZE + style.margin.vertical + style.border.vertical + style.padding.vertical);
                var container = new IMGUIContainer(() => {
                    EditorGUILayout.LabelField(label, style, GUILayout.Width(resolvedStyle.width), layoutHeight);
                });
                Add(container);
            }

            private static void RemoveHorizontalSpace(GUIStyle style)
            {
                style.margin.left = 0;
                style.margin.right = 0;
                style.border.left = 0;
                style.border.right = 0;
                style.padding.left = 0;
                style.padding.right = 0;
            }
        }
        #endif

        internal class MDList : VisualElement
        {
            private MDLabel veMarker;
            private MDLabel veLabel;
            internal MDList(string label, string marker, bool enableSpace, int depth)
            {
                style.flexDirection = FlexDirection.Row;
                style.marginLeft = depth * 24;
                if(enableSpace) style.marginTop = 12;

                veMarker = new MDLabel(marker);
                veMarker.style.unityTextAlign = TextAnchor.UpperRight;
                veMarker.style.width = 24;
                veMarker.style.paddingTop = veMarker.style.paddingTop.value.value + 1;
                Add(veMarker);

                veLabel = new MDLabel(label, MDType.p, false);
                veLabel.style.paddingRight = veLabel.style.paddingRight.value.value + 20;
                veLabel.style.flexGrow = 1;
                Add(veLabel);
            }
        }

        internal class Blockquote : Box
        {
            internal Blockquote(bool enableSpace)
            {
                style.backgroundColor = new Color(0.5f,0.5f,0.5f,0.1f);
                style.paddingLeft = 8;
                style.borderLeftColor = new Color(0.196f,0.352f,0.592f,1);
                style.borderLeftWidth = 2;
                style.borderTopWidth = 0;
                style.borderBottomWidth = 0;
                style.borderRightWidth = 0;
                if(enableSpace)
                {
                    style.marginTop = 12;
                }
            }
        }

        internal static VisualElement Draw(string markdown)
        {
            var root = new VisualElement();
            var mds = Get(markdown);
            var prevtype = MDType.p;
            int prevDepth = 0;
            var listCounts = new Dictionary<int,int>();
            foreach(var mdpart in mds)
            {
                bool enableSpace = true;
                switch(mdpart.Item1)
                {
                    case MDType.p:
                    case MDType.ul:
                    case MDType.ol:
                        enableSpace = prevtype != mdpart.Item1;
                        break;
                }
                if(mdpart.Item3 > prevDepth)
                {
                    enableSpace = true;
                    for(int i = 0; i < mdpart.Item3 - prevDepth; i++)
                    {
                        var b = new Blockquote(enableSpace);
                        root.Add(b);
                        root = b;
                        enableSpace = false;
                    }
                }
                else if(mdpart.Item3 < prevDepth)
                {
                    for(int i = 0; i < prevDepth - mdpart.Item3; i++)
                    {
                        root = root.parent;
                    }
                    enableSpace = true;
                }
                switch(mdpart.Item1)
                {
                    case MDType.ul:
                        root.Add(new MDList(mdpart.Item2, "・ ", enableSpace, mdpart.Item4));
                        break;
                    case MDType.ol:
                        int count = 1;
                        if(listCounts.ContainsKey(mdpart.Item4))
                        {
                            count = ++listCounts[mdpart.Item4];
                        }
                        else
                        {
                            listCounts[0] = 1;
                        }
                        root.Add(new MDList(mdpart.Item2, $"{count}. ", enableSpace, mdpart.Item4));
                        break;
                    default:
                        if(prevtype == MDType.ol) listCounts.Clear();
                        root.Add(new MDLabel(mdpart.Item2, mdpart.Item1, enableSpace));
                        break;
                }
                prevtype = mdpart.Item1;
                prevDepth = mdpart.Item3;
            }
            return root;
        }

        private static List<(MDType,string,int,int)> Get(string markdown)
        {
            if(mdlist.ContainsKey(markdown)) return mdlist[markdown];
            var temp = new List<(MDType,string,int,int)>();
            var sr = new StringReader(markdown);
            string line;
            var sb = new StringBuilder();
            int prevDepth = 0;
            bool isNewline = false;
            int listSpace = 1;
            while((line = sr.ReadLine()) != null)
            {
                var mdpart = CheckType(line);
                if(mdpart.type == MDType.p)
                {
                    if(prevDepth != mdpart.blockquoteDepth) isNewline = temp.AddFixed((MDType.p,sb,prevDepth,0));
                    if(!isNewline) sb.Append(" ");
                    sb.Append(mdpart.text);
                    isNewline = false;
                    if(mdpart.isBr && sb.Length > 0)
                    {
                        sb.AppendLine();
                        isNewline = true;
                    }
                }
                else if(mdpart.type == MDType.ul || mdpart.type == MDType.ol)
                {
                    isNewline = temp.AddFixed((MDType.p,sb,prevDepth,0));
                    if(listSpace == 1 && mdpart.listDepth > 1) listSpace = mdpart.listDepth;
                    temp.Add((mdpart.type, mdpart.text, mdpart.blockquoteDepth, mdpart.listDepth / listSpace));
                }
                else if(mdpart.type != MDType.br && mdpart.type != MDType.h1line && mdpart.type != MDType.h2line)
                {
                    isNewline = temp.AddFixed((MDType.p,sb,prevDepth,0));
                    temp.Add((mdpart.type, mdpart.text, mdpart.blockquoteDepth, 0));
                }
                else if(mdpart.type == MDType.br)
                {
                    if(prevDepth != mdpart.blockquoteDepth) isNewline = temp.AddFixed((MDType.p,sb,prevDepth,0));
                    else if(sb.Length > 0)
                    {
                        sb.AppendLine();
                        isNewline = true;
                    }
                }
                else
                {
                    if(mdpart.type == MDType.h1line) isNewline = temp.AddFixed((MDType.h1,sb,prevDepth,0));
                    if(mdpart.type == MDType.h2line) isNewline = temp.AddFixed((MDType.h2,sb,prevDepth,0));
                }
                if(mdpart.type != MDType.ul && mdpart.type != MDType.ol) listSpace = 1;
                prevDepth = mdpart.blockquoteDepth;
            }
            temp.AddFixed((MDType.p,sb,prevDepth,0));
            return mdlist[markdown] = temp;
        }

        private static bool AddFixed(this List<(MDType, string, int, int)> list, (MDType, StringBuilder, int, int) item)
        {
            if(item.Item2.Length == 0) return false;
            var s = item.Item2.ToString();
            item.Item2.Clear();
            list.AddFixed((item.Item1, s, item.Item3, item.Item4));
            return true;
        }

        private static void AddFixed(this List<(MDType, string, int, int)> list, (MDType, string, int, int) item)
        {
            var s = item.Item2.Trim();
            bool hasMultiByte = s.Length != encSjis.GetByteCount(s);
            if(hasMultiByte) s = s.Replace(" ", "\u00A0");
            s = s.Replace("<br>", System.Environment.NewLine);
            s = s.Replace("``", string.Empty);
            ReplaceSyntax(ref s, "**", "<b>", "</b>");
            ReplaceSyntax(ref s, "__", "<b>", "</b>");
            ReplaceSyntax(ref s, "*", "<i>", "</i>");
            ReplaceSyntax(ref s, "_", "<i>", "</i>");
            if(hasMultiByte) ReplaceSyntax(ref s, "`", "\u00A0<color=#2d9c63ff>", "</color>\u00A0");
            else ReplaceSyntax(ref s, "`", " <color=#2d9c63ff>", "</color> ");
            ReplaceMDLinks(ref s);
            ReplaceLinks(ref s);
            #if !UNITY_2022_3_OR_NEWER
            s = s.Replace("<a href=", "<a lilhref=");
            #endif
            list.Add((item.Item1,s,item.Item3,item.Item4));
        }

        private static void ReplaceSyntax(ref string s, string syntax, string start, string end)
        {
            while(true)
            {
                var first = s.IndexOf(syntax);
                if(first == -1) return;

                var length = syntax.Length;
                var second = s.IndexOf(syntax, first + length);
                if(second == -1) return;

                s = s.Remove(first) + start + s.Substring(first + length);
                var second2 = s.IndexOf(syntax);
                s = s.Remove(second2) + end + s.Substring(second2 + length);
            }
        }

        private static void ReplaceMDLinks(ref string s)
        {
            s = Regex.Replace(s, @"\[([^\]]+)\]\(([^)]+)\)", m =>  $"<a href=\"{m.Groups[2].Value}\">{m.Groups[1].Value}</a>");
        }

        private static void ReplaceLinks(ref string s)
        {
            s = Regex.Replace(s, @"(?<!<a href="")https?://[^\s\n\\\(\)\^\[\]`<>#""%（）{}|]*", m =>  $"<a href=\"{m.Value}\">{m.Value}</a>");
        }

        private static MDPart CheckType(string line)
        {
            bool isBr = line.EndsWith("  ");
            var trim = line.Trim();
            var depth = GetBlockquoteDepth(ref trim);

            if(trim.StartsWith("###### "))
                return new MDPart(MDType.h6, trim.Substring(7), isBr, depth);
            if(trim.StartsWith("##### "))
                return new MDPart(MDType.h5, trim.Substring(6), isBr, depth);
            if(trim.StartsWith("#### "))
                return new MDPart(MDType.h4, trim.Substring(5), isBr, depth);
            if(trim.StartsWith("### "))
                return new MDPart(MDType.h3, trim.Substring(4), isBr, depth);
            if(trim.StartsWith("## "))
                return new MDPart(MDType.h2, trim.Substring(3), isBr, depth);
            if(trim.StartsWith("# "))
                return new MDPart(MDType.h1, trim.Substring(2), isBr, depth);
            if(trim.StartsWith("- "))
                return new MDPart(MDType.ul, trim.Substring(2), isBr, depth, GetListDepth(line));
            if(trim.StartsWith("+ "))
                return new MDPart(MDType.ul, trim.Substring(2), isBr, depth, GetListDepth(line));
            if(trim.StartsWith("* "))
                return new MDPart(MDType.ul, trim.Substring(2), isBr, depth, GetListDepth(line));
            if(trim.StartsWith("=") && !trim.Any(c => c != '='))
                return new MDPart(MDType.h1line, "", isBr, depth);
            if(trim.StartsWith("-") && !trim.Any(c => c != '-'))
                return new MDPart(MDType.h2line, "", isBr, depth);
            if(string.IsNullOrEmpty(trim))
                return new MDPart(MDType.br, "", isBr, depth);

            var matchOL = Regex.Match(trim, @"\d+. ");
            if(matchOL.Success && matchOL.Index == 0)
                return new MDPart(MDType.ol, Regex.Replace(trim, @"\d+. ", ""), isBr, depth, GetListDepth(line));

            return new MDPart(MDType.p, trim, isBr, depth);
        }

        private static int GetBlockquoteDepth(ref string line)
        {
            int i = 0;
            while(true)
            {
                if(!line.StartsWith(">")) return i;
                i++;
                line = line.Substring(1).TrimStart();
            }
        }

        private static int GetListDepth(string line)
        {
            return line.Length - line.TrimStart().Length;
        }

        internal struct MDPart
        {
            internal MDType type;
            internal string text;
            internal bool isBr;
            internal int blockquoteDepth;
            internal int listDepth;

            internal MDPart(MDType type, string text, bool isBr, int blockquoteDepth, int listDepth = 0)
            {
                this.type = type;
                this.text = text;
                this.isBr = isBr;
                this.blockquoteDepth = blockquoteDepth;
                this.listDepth = listDepth;
            }
        }
    }
}
