﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AutocompleteMenuNS;
using ScintillaNET;

namespace MarkMpn.XmlSchemaAutocomplete.Scintilla
{
    class ScintillaXmlHelper
    {
        private bool _skipQuote;
        private ScintillaNET.Scintilla _scintilla;
        private AutocompleteMenu _menu;

        public ScintillaXmlHelper(ScintillaNET.Scintilla scintilla, Autocomplete autocomplete)
        {
            _menu = new AutocompleteMenu();
            _menu.MinFragmentLength = 0;
            _menu.AllowsTabKey = true;
            _menu.AppearInterval = 100;
            _menu.TargetControlWrapper = new ScintillaWrapper(scintilla);
            _menu.Font = new Font(scintilla.Styles[Style.Default].Font, scintilla.Styles[Style.Default].SizeF);
            //_menu.ImageList = _images;
            _menu.MaximumSize = new Size(1000, _menu.MaximumSize.Height);
            _menu.SearchPattern = "[\\w<\"'\\-:/]";

            _menu.SetAutocompleteItems(new XmlSchemaAutocompleteItemSource(autocomplete, scintilla));

            _scintilla = scintilla;
        }

        public void Attach()
        {
            _scintilla.InsertCheck += InsertCheck;
            _scintilla.KeyUp += KeyUp;

            _scintilla.KeyDown += KeyDown;
        }

        private void KeyUp(object sender, KeyEventArgs e)
        {
            var scintilla = (ScintillaNET.Scintilla)sender;

            if (_skipQuote)
            {
                scintilla.SelectionStart++;
                _skipQuote = false;
            }
        }

        private void InsertCheck(object sender, InsertCheckEventArgs e)
        {
            var scintilla = (ScintillaNET.Scintilla)sender;
            _skipQuote = false;

            if (e.Text.EndsWith("\"") && e.Position < scintilla.TextLength && scintilla.GetCharAt(e.Position) == '"')
            {
                e.Text = e.Text.Substring(0, e.Text.Length - 1);
                _skipQuote = true;
            }
        }

        public string Indent { get; set; } = "  ";

        private void KeyDown(object sender, KeyEventArgs e)
        {
            var scintilla = (ScintillaNET.Scintilla)sender;

            if (e.KeyCode == Keys.Back && scintilla.SelectedText == String.Empty)
            {
                // Backspace deletes entire indent level
                var lineIndex = scintilla.LineFromPosition(scintilla.SelectionStart);
                var line = scintilla.Lines[lineIndex];
                if (scintilla.SelectionStart == line.Position + line.Length && line.Text.EndsWith(Indent))
                {
                    scintilla.SelectionStart -= Indent.Length;
                    scintilla.SelectionEnd = scintilla.SelectionStart + Indent.Length;
                    scintilla.ReplaceSelection("");
                    e.Handled = true;
                }
            }

            if (e.KeyCode == Keys.Oem2)
            {
                // Starting an end tag decreases indent
                var lineIndex = scintilla.LineFromPosition(scintilla.SelectionStart);
                var line = scintilla.Lines[lineIndex];
                if (scintilla.SelectionStart == line.Position + line.Length && line.Text.EndsWith(Indent + "<"))
                {
                    scintilla.SelectionStart -= Indent.Length + 1;
                    scintilla.SelectionEnd = scintilla.SelectionStart + Indent.Length + 1;
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
                    insertText += Indent;

                if (lineAfterPos.StartsWith("</"))
                    appendText += Environment.NewLine + currentIndent;

                var pos = scintilla.SelectionStart;
                scintilla.ReplaceSelection(insertText + appendText);
                scintilla.SelectionStart = pos + insertText.Length;
                scintilla.SelectionEnd = pos + insertText.Length;

                e.Handled = true;
                e.SuppressKeyPress = true;
            }

            if (e.KeyCode == Keys.Oemplus)
            {
                // Auto-add quotes when pressing equals
                var parser = new PartialXmlReader(scintilla.GetTextRange(0, scintilla.SelectionStart) + "=");
                PartialXmlNode lastNode = null;
                while (parser.TryRead(out var node))
                    lastNode = node;

                if (parser.State == ReaderState.InAttributeEquals)
                {
                    var pos = scintilla.SelectionStart;
                    scintilla.ReplaceSelection("=\"\"");
                    scintilla.SelectionStart = pos + 2;
                    scintilla.SelectionEnd = pos + 2;

                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
        }
    }
}