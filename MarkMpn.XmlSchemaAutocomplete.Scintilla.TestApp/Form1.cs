using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using AutocompleteMenuNS;
using ScintillaNET;

namespace MarkMpn.XmlSchemaAutocomplete.Scintilla.TestApp
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            InitializeScintilla();
            InitializeAutocomplete();
        }

        private void InitializeAutocomplete()
        {
            var autocomplete = new Autocomplete<FetchType>();
            autocomplete.AddTypeDescription<condition>("Filter Condition", "Filters the data based on a particular attribute");
            autocomplete.AddMemberDescription<FetchType>(nameof(FetchType.top), "Top Count", "Limits the number of records returned");

            var menu = new AutocompleteMenu();
            menu.MinFragmentLength = 0;
            menu.AllowsTabKey = true;
            menu.AppearInterval = 100;
            menu.TargetControlWrapper = new ScintillaWrapper(scintilla);
            menu.Font = new Font(scintilla.Styles[Style.Default].Font, scintilla.Styles[Style.Default].SizeF);
            //menu.ImageList = _images;
            menu.MaximumSize = new Size(1000, menu.MaximumSize.Height);
            menu.SearchPattern = "[\\w<\"'\\-:/]";

            menu.SetAutocompleteItems(new XmlSchemaAutocompleteItemSource(autocomplete, scintilla));

            const string indent = "  ";

            scintilla.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Back && scintilla.SelectedText == String.Empty)
                {
                    // Backspace deletes entire indent level
                    var lineIndex = scintilla.LineFromPosition(scintilla.SelectionStart);
                    var line = scintilla.Lines[lineIndex];
                    if (scintilla.SelectionStart == line.Position + line.Length && line.Text.EndsWith(indent))
                    {
                        scintilla.SelectionStart -= indent.Length;
                        scintilla.SelectionEnd = scintilla.SelectionStart + indent.Length;
                        scintilla.ReplaceSelection("");
                        e.Handled = true;
                    }
                }

                if (e.KeyCode == Keys.Oem2)
                {
                    // Starting an end tag decreases indent
                    var lineIndex = scintilla.LineFromPosition(scintilla.SelectionStart);
                    var line = scintilla.Lines[lineIndex];
                    if (scintilla.SelectionStart == line.Position + line.Length && line.Text.EndsWith(indent + "<"))
                    {
                        scintilla.SelectionStart -= indent.Length + 1;
                        scintilla.SelectionEnd = scintilla.SelectionStart + indent.Length + 1;
                        scintilla.ReplaceSelection("<");
                        e.Handled = true;
                    }
                }

                if (e.KeyCode == Keys.OemPeriod)
                {
                    // Auto-close elements
                    var parser = new PartialXmlReader(scintilla.GetTextRange(0, scintilla.SelectionStart) + ">");
                    PartialXmlNode lastNode = null;
                    while (parser.TryRead(out var node))
                        lastNode = node;

                    if (lastNode is PartialXmlElement lastElement && parser.State == ReaderState.InText && !lastElement.SelfClosing)
                    {
                        var pos = scintilla.SelectionStart;
                        scintilla.ReplaceSelection($"</{lastElement.Name}>");
                        scintilla.SelectionStart = pos;
                        scintilla.SelectionEnd = pos;
                    }
                }

                if (e.KeyCode == Keys.Return)
                {
                    // Auto-indent new lines
                    var insertText = Environment.NewLine;
                    var appendText = "";

                    var currentLine = scintilla.Lines[scintilla.LineFromPosition(scintilla.CurrentPosition)];
                    var currentIndent = Regex.Match(currentLine.Text, "^[ \t]*").Value;

                    insertText += currentIndent;

                    var lineToPos = currentLine.Text.Substring(0, scintilla.CurrentPosition - currentLine.Position);
                    var lineAfterPos = currentLine.Text.Substring(scintilla.CurrentPosition - currentLine.Position);

                    if (lineToPos.EndsWith(">") && !lineToPos.EndsWith("/>") && !lineToPos.TrimStart().StartsWith("</"))
                        insertText += indent;

                    if (lineAfterPos.StartsWith("</"))
                        appendText += Environment.NewLine + currentIndent;

                    var pos = scintilla.SelectionStart;
                    scintilla.ReplaceSelection(insertText + appendText);
                    scintilla.SelectionStart = pos + insertText.Length;
                    scintilla.SelectionEnd = pos + insertText.Length;

                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
        }

        private void InitializeScintilla()
        {
            // Ref: https://gist.github.com/anonymous/63036aa8c1cefcfcb013

            // Reset the styles
            scintilla.StyleResetDefault();
            scintilla.Styles[Style.Default].Font = "Consolas";
            scintilla.Styles[Style.Default].Size = 10;
            scintilla.StyleClearAll();

            // Set the XML Lexer
            scintilla.Lexer = Lexer.Xml;

            // Show line numbers
            scintilla.Margins[0].Width = 20;

            // Enable folding
            scintilla.SetProperty("fold", "1");
            scintilla.SetProperty("fold.compact", "1");
            scintilla.SetProperty("fold.html", "1");

            // Use Margin 2 for fold markers
            scintilla.Margins[2].Type = MarginType.Symbol;
            scintilla.Margins[2].Mask = Marker.MaskFolders;
            scintilla.Margins[2].Sensitive = true;
            scintilla.Margins[2].Width = 20;

            // Reset folder markers
            for (int i = Marker.FolderEnd; i <= Marker.FolderOpen; i++)
            {
                scintilla.Markers[i].SetForeColor(SystemColors.ControlLightLight);
                scintilla.Markers[i].SetBackColor(SystemColors.ControlDark);
            }

            // Style the folder markers
            scintilla.Markers[Marker.Folder].Symbol = MarkerSymbol.BoxPlus;
            scintilla.Markers[Marker.Folder].SetBackColor(SystemColors.ControlText);
            scintilla.Markers[Marker.FolderOpen].Symbol = MarkerSymbol.BoxMinus;
            scintilla.Markers[Marker.FolderEnd].Symbol = MarkerSymbol.BoxPlusConnected;
            scintilla.Markers[Marker.FolderEnd].SetBackColor(SystemColors.ControlText);
            scintilla.Markers[Marker.FolderMidTail].Symbol = MarkerSymbol.TCorner;
            scintilla.Markers[Marker.FolderOpenMid].Symbol = MarkerSymbol.BoxMinusConnected;
            scintilla.Markers[Marker.FolderSub].Symbol = MarkerSymbol.VLine;
            scintilla.Markers[Marker.FolderTail].Symbol = MarkerSymbol.LCorner;

            // Enable automatic folding
            scintilla.AutomaticFold = AutomaticFold.Show | AutomaticFold.Click | AutomaticFold.Change;

            // Set the Styles
            scintilla.StyleResetDefault();
            // I like fixed font for XML
            scintilla.Styles[Style.Default].Font = "Courier New";
            scintilla.Styles[Style.Default].Size = 10;
            scintilla.StyleClearAll();
            scintilla.Styles[Style.Xml.Attribute].ForeColor = Color.Red;
            scintilla.Styles[Style.Xml.Entity].ForeColor = Color.Red;
            scintilla.Styles[Style.Xml.Comment].ForeColor = Color.Green;
            scintilla.Styles[Style.Xml.Tag].ForeColor = Color.Blue;
            scintilla.Styles[Style.Xml.TagEnd].ForeColor = Color.Blue;
            scintilla.Styles[Style.Xml.DoubleString].ForeColor = Color.DeepPink;
            scintilla.Styles[Style.Xml.SingleString].ForeColor = Color.DeepPink;
        }
    }
}
