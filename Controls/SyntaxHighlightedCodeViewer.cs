using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Documents;
using Serilog;

namespace SchoolOrganizer.Controls;

/// <summary>
/// A custom control that provides syntax highlighting for code files with colors
/// </summary>
public class SyntaxHighlightedCodeViewer : UserControl
{
    private ScrollViewer? _scrollViewer;
    private TextBlock? _textBlock;
    private string _codeContent = string.Empty;
    private string _fileExtension = string.Empty;

    public static readonly StyledProperty<string?> CodeContentProperty =
        AvaloniaProperty.Register<SyntaxHighlightedCodeViewer, string?>(nameof(CodeContent));

    public static readonly StyledProperty<string> FileExtensionProperty =
        AvaloniaProperty.Register<SyntaxHighlightedCodeViewer, string>(nameof(FileExtension));

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<SyntaxHighlightedCodeViewer, bool>(nameof(IsReadOnly), true);

    public string? CodeContent
    {
        get => GetValue(CodeContentProperty);
        set
        {
            Log.Debug("SyntaxHighlightedCodeViewer: CodeContent setter called with length: {Length}", value?.Length ?? 0);
            SetValue(CodeContentProperty, value);
        }
    }

    public string FileExtension
    {
        get => GetValue(FileExtensionProperty);
        set
        {
            Log.Debug("SyntaxHighlightedCodeViewer: FileExtension setter called with: {Extension}", value);
            SetValue(FileExtensionProperty, value);
        }
    }

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public SyntaxHighlightedCodeViewer()
    {
        Log.Debug("SyntaxHighlightedCodeViewer: Constructor called");
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        _textBlock = new TextBlock
        {
            FontFamily = new FontFamily("Consolas, Courier New, monospace"),
            FontSize = 12,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Foreground = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(8)
        };

        _scrollViewer = new ScrollViewer
        {
            Content = _textBlock
        };

        Content = _scrollViewer;

        // Subscribe to property changes
        CodeContentProperty.Changed.Subscribe(OnCodeContentChanged);
        FileExtensionProperty.Changed.Subscribe(OnFileExtensionChanged);
    }

    private void OnCodeContentChanged(AvaloniaPropertyChangedEventArgs<string?> e)
    {
        if (_textBlock != null && e.NewValue.HasValue && e.NewValue.Value != null)
        {
            _codeContent = e.NewValue.Value;
            Log.Debug("SyntaxHighlightedCodeViewer: CodeContent changed, length: {Length}", _codeContent.Length);
            ApplySyntaxHighlighting();
        }
        else
        {
            Log.Warning("SyntaxHighlightedCodeViewer: CodeContent is null or empty");
        }
    }

    private void OnFileExtensionChanged(AvaloniaPropertyChangedEventArgs<string> e)
    {
        if (e.NewValue.HasValue && e.NewValue.Value != null)
        {
            _fileExtension = e.NewValue.Value;
            Log.Debug("SyntaxHighlightedCodeViewer: FileExtension changed to: {Extension}", _fileExtension);
            ApplySyntaxHighlighting();
        }
    }

    private void ApplySyntaxHighlighting()
    {
        var currentCodeContent = GetValue(CodeContentProperty) ?? string.Empty;
        var currentFileExtension = GetValue(FileExtensionProperty) ?? string.Empty;

        if (_textBlock == null || string.IsNullOrEmpty(currentCodeContent))
        {
            return;
        }

        try
        {
            var extension = currentFileExtension.ToLowerInvariant().TrimStart('.');

            Log.Debug("SyntaxHighlightedCodeViewer: Applying highlighting for extension: {Extension}", extension);

            var highlightedText = GetHighlightedText(currentCodeContent, extension);
            if (_textBlock?.Inlines != null)
            {
                _textBlock.Inlines.Clear();
                _textBlock.Inlines.AddRange(highlightedText);
            }

            Log.Debug("SyntaxHighlightedCodeViewer: Applied {InlineCount} inlines", highlightedText.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error applying syntax highlighting");
            _textBlock.Text = currentCodeContent;
        }
    }

    private InlineCollection GetHighlightedText(string code, string extension)
    {
        return extension switch
        {
            "java" or "cs" or "cpp" or "c" or "cxx" or "cc" => HighlightCSharpStyleCode(code),
            "py" or "python" => HighlightPythonCode(code),
            "js" or "javascript" => HighlightJavaScriptCode(code),
            "html" or "htm" => HighlightHtmlCode(code),
            "css" => HighlightCssCode(code),
            "xml" => HighlightXmlCode(code),
            "json" => HighlightJsonCode(code),
            _ => CreateDefaultInlines(code)
        };
    }

    private InlineCollection CreateDefaultInlines(string code)
    {
        var inlines = new InlineCollection();
        inlines.Add(new Run(code) { Foreground = new SolidColorBrush(Color.FromRgb(248, 248, 248)) });
        return inlines;
    }

    private InlineCollection HighlightCSharpStyleCode(string code)
    {
        var inlines = new InlineCollection();
        var lines = code.Split('\n');

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                inlines.Add(new LineBreak());
                continue;
            }

            var keywordPattern = @"\b(public|private|protected|static|void|int|string|bool|char|double|float|long|short|byte|if|else|for|while|do|switch|case|default|break|continue|return|class|interface|enum|struct|namespace|using|import|package|extends|implements|new|this|super|final|abstract|virtual|override|sealed|const|readonly|volatile|extern|unsafe|fixed|lock|try|catch|finally|throw|throws|goto|sizeof|typeof|is|as|checked|unchecked|default|delegate|event|operator|explicit|implicit|params|ref|out|in|var|dynamic|async|await|yield|get|set|add|remove|value|partial|where|select|from|group|orderby|join|let|into|on|equals|by|ascending|descending)\b";
            var stringPattern = @"""[^""]*""|'[^']*'";
            var commentPattern = @"//.*$|/\*[\s\S]*?\*/";
            var numberPattern = @"\b\d+\.?\d*\b";

            var processedLine = ProcessLineWithPatterns(line, new[]
            {
                (commentPattern, Color.FromRgb(106, 153, 85)),
                (stringPattern, Color.FromRgb(206, 145, 120)),
                (keywordPattern, Color.FromRgb(86, 156, 214)),
                (numberPattern, Color.FromRgb(181, 206, 168))
            });

            inlines.AddRange(processedLine);
            inlines.Add(new LineBreak());
        }

        return inlines;
    }

    private InlineCollection HighlightPythonCode(string code)
    {
        var inlines = new InlineCollection();
        var lines = code.Split('\n');

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                inlines.Add(new LineBreak());
                continue;
            }

            var keywordPattern = @"\b(def|class|if|elif|else|for|while|try|except|finally|with|as|import|from|return|yield|lambda|and|or|not|in|is|None|True|False|pass|break|continue|raise|assert|del|global|nonlocal)\b";
            var stringPattern = @"""[^""]*""|'[^']*'|'''[\s\S]*?'''|""""""[\s\S]*?""""""";
            var commentPattern = @"#.*$";
            var numberPattern = @"\b\d+\.?\d*\b";

            var processedLine = ProcessLineWithPatterns(line, new[]
            {
                (commentPattern, Color.FromRgb(106, 153, 85)),
                (stringPattern, Color.FromRgb(206, 145, 120)),
                (keywordPattern, Color.FromRgb(86, 156, 214)),
                (numberPattern, Color.FromRgb(181, 206, 168))
            });

            inlines.AddRange(processedLine);
            inlines.Add(new LineBreak());
        }

        return inlines;
    }

    private InlineCollection HighlightJavaScriptCode(string code)
    {
        var inlines = new InlineCollection();
        var lines = code.Split('\n');

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                inlines.Add(new LineBreak());
                continue;
            }

            var keywordPattern = @"\b(function|var|let|const|if|else|for|while|do|switch|case|default|break|continue|return|try|catch|finally|throw|new|this|class|extends|import|export|from|default|async|await|yield|typeof|instanceof|in|of|delete|void|true|false|null|undefined)\b";
            var stringPattern = @"""[^""]*""|'[^']*'|`[^`]*`";
            var commentPattern = @"//.*$|/\*[\s\S]*?\*/";
            var numberPattern = @"\b\d+\.?\d*\b";

            var processedLine = ProcessLineWithPatterns(line, new[]
            {
                (commentPattern, Color.FromRgb(106, 153, 85)),
                (stringPattern, Color.FromRgb(206, 145, 120)),
                (keywordPattern, Color.FromRgb(86, 156, 214)),
                (numberPattern, Color.FromRgb(181, 206, 168))
            });

            inlines.AddRange(processedLine);
            inlines.Add(new LineBreak());
        }

        return inlines;
    }

    private InlineCollection HighlightHtmlCode(string code)
    {
        var inlines = new InlineCollection();
        var lines = code.Split('\n');

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                inlines.Add(new LineBreak());
                continue;
            }

            var tagPattern = @"<[^>]*>";
            var attributePattern = @"\w+\s*=";
            var commentPattern = @"<!--[\s\S]*?-->";

            var processedLine = ProcessLineWithPatterns(line, new[]
            {
                (commentPattern, Color.FromRgb(106, 153, 85)),
                (tagPattern, Color.FromRgb(86, 156, 214)),
                (attributePattern, Color.FromRgb(206, 145, 120))
            });

            inlines.AddRange(processedLine);
            inlines.Add(new LineBreak());
        }

        return inlines;
    }

    private InlineCollection HighlightCssCode(string code)
    {
        var inlines = new InlineCollection();
        var lines = code.Split('\n');

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                inlines.Add(new LineBreak());
                continue;
            }

            var selectorPattern = @"[.#]?[\w-]+\s*\{";
            var propertyPattern = @"\w+:\s*";
            var valuePattern = @":[^;]+";
            var commentPattern = @"/\*[\s\S]*?\*/";

            var processedLine = ProcessLineWithPatterns(line, new[]
            {
                (commentPattern, Color.FromRgb(106, 153, 85)),
                (selectorPattern, Color.FromRgb(86, 156, 214)),
                (propertyPattern, Color.FromRgb(206, 145, 120)),
                (valuePattern, Color.FromRgb(181, 206, 168))
            });

            inlines.AddRange(processedLine);
            inlines.Add(new LineBreak());
        }

        return inlines;
    }

    private InlineCollection HighlightXmlCode(string code)
    {
        return HighlightHtmlCode(code);
    }

    private InlineCollection HighlightJsonCode(string code)
    {
        var inlines = new InlineCollection();
        var lines = code.Split('\n');

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                inlines.Add(new LineBreak());
                continue;
            }

            var keyPattern = @"""[^""]*""\s*:";
            var stringPattern = @"""[^""]*""";
            var numberPattern = @"\b\d+\.?\d*\b";
            var booleanPattern = @"\b(true|false|null)\b";

            var processedLine = ProcessLineWithPatterns(line, new[]
            {
                (keyPattern, Color.FromRgb(86, 156, 214)),
                (stringPattern, Color.FromRgb(206, 145, 120)),
                (numberPattern, Color.FromRgb(181, 206, 168)),
                (booleanPattern, Color.FromRgb(86, 156, 214))
            });

            inlines.AddRange(processedLine);
            inlines.Add(new LineBreak());
        }

        return inlines;
    }

    private InlineCollection ProcessLineWithPatterns(string line, (string pattern, Color color)[] patterns)
    {
        var inlines = new InlineCollection();
        var matches = new List<(int start, int length, Color color)>();

        foreach (var (pattern, color) in patterns)
        {
            var regex = new Regex(pattern);
            foreach (Match match in regex.Matches(line))
            {
                matches.Add((match.Index, match.Length, color));
            }
        }

        matches.Sort((a, b) => a.start.CompareTo(b.start));

        int currentIndex = 0;
        foreach (var (start, length, color) in matches)
        {
            if (start > currentIndex)
            {
                var beforeText = line.Substring(currentIndex, start - currentIndex);
                inlines.Add(new Run(beforeText) { Foreground = new SolidColorBrush(Color.FromRgb(248, 248, 248)) });
            }

            var matchText = line.Substring(start, length);
            inlines.Add(new Run(matchText) { Foreground = new SolidColorBrush(color) });

            currentIndex = start + length;
        }

        if (currentIndex < line.Length)
        {
            var remainingText = line.Substring(currentIndex);
            inlines.Add(new Run(remainingText) { Foreground = new SolidColorBrush(Color.FromRgb(248, 248, 248)) });
        }

        return inlines;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _textBlock = null;
        _scrollViewer = null;
        base.OnDetachedFromVisualTree(e);
    }
}
