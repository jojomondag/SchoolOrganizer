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
/// A custom control that provides syntax highlighting for code files with text selection capability using SelectableTextBlock
/// </summary>
public class SyntaxHighlightedCodeViewer : UserControl
{
    private SelectableTextBlock? _textBlock;
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
            Log.Information("SyntaxHighlightedCodeViewer: CodeContent setter called with length: {Length}", value?.Length ?? 0);
            SetValue(CodeContentProperty, value);
        }
    }

    public string FileExtension
    {
        get => GetValue(FileExtensionProperty);
        set
        {
            Log.Information("SyntaxHighlightedCodeViewer: FileExtension setter called with: {Extension}", value);
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
        Log.Information("SyntaxHighlightedCodeViewer: Constructor called");
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        _textBlock = new SelectableTextBlock
        {
            FontFamily = new FontFamily("Consolas, Courier New, monospace"),
            FontSize = 12,
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            Foreground = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MinHeight = 50,
            MinWidth = 100,
            IsVisible = true
        };

        Content = _textBlock;

        // Subscribe to property changes
        CodeContentProperty.Changed.Subscribe(OnCodeContentChanged);
        FileExtensionProperty.Changed.Subscribe(OnFileExtensionChanged);
        
        Log.Information("SyntaxHighlightedCodeViewer: InitializeComponent completed");
    }

    private void OnCodeContentChanged(AvaloniaPropertyChangedEventArgs<string?> e)
    {
        Log.Information("SyntaxHighlightedCodeViewer: OnCodeContentChanged called, NewValue: {HasValue}, Length: {Length}", 
            e.NewValue.HasValue, e.NewValue.Value?.Length ?? 0);
        
        if (_textBlock != null && e.NewValue.HasValue && e.NewValue.Value != null)
        {
            _codeContent = e.NewValue.Value;
            Log.Information("SyntaxHighlightedCodeViewer: CodeContent changed, length: {Length}", _codeContent.Length);
            ApplySyntaxHighlighting();
        }
        else
        {
            Log.Warning("SyntaxHighlightedCodeViewer: CodeContent is null or empty");
            if (_textBlock != null)
            {
                _textBlock.Inlines?.Clear();
                Log.Information("SyntaxHighlightedCodeViewer: TextBlock cleared");
            }
        }
    }

    private void OnFileExtensionChanged(AvaloniaPropertyChangedEventArgs<string> e)
    {
        if (e.NewValue.HasValue && e.NewValue.Value != null)
        {
            _fileExtension = e.NewValue.Value;
            Log.Information("SyntaxHighlightedCodeViewer: FileExtension changed to: {Extension}", _fileExtension);
            if (_textBlock != null && !string.IsNullOrEmpty(_codeContent))
            {
            ApplySyntaxHighlighting();
            }
        }
    }

    private void ApplySyntaxHighlighting()
    {
        if (_textBlock == null || string.IsNullOrEmpty(_codeContent))
        {
            return;
        }

        try
        {
            var extension = _fileExtension.ToLowerInvariant().TrimStart('.');
            Log.Information("SyntaxHighlightedCodeViewer: Applying highlighting for extension: {Extension}", extension);

            var inlines = new InlineCollection();
            var language = GetLanguageFromExtension(extension);
            
            if (language != Language.Unknown)
            {
                var highlightedRuns = ParseCodeWithSyntaxHighlighting(_codeContent, language);
                foreach (var run in highlightedRuns)
                {
                    inlines.Add(run);
                }
            }
            else
            {
                // Fallback to plain text
                inlines.Add(new Run(_codeContent) { Foreground = new SolidColorBrush(Color.FromRgb(248, 248, 248)) });
            }

            _textBlock.Inlines = inlines;
            Log.Information("SyntaxHighlightedCodeViewer: Applied highlighting with {Count} runs", inlines.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error applying syntax highlighting");
            // Fallback to plain text
            _textBlock.Inlines = new InlineCollection { new Run(_codeContent) { Foreground = new SolidColorBrush(Color.FromRgb(248, 248, 248)) } };
        }
    }

    private Language GetLanguageFromExtension(string extension)
    {
        return extension switch
        {
            "cs" or "csharp" => Language.CSharp,
            "java" => Language.Java,
            "py" or "python" => Language.Python,
            "js" or "javascript" => Language.JavaScript,
            "ts" or "typescript" => Language.TypeScript,
            "html" or "htm" => Language.HTML,
            "css" => Language.CSS,
            "json" => Language.JSON,
            "xml" => Language.XML,
            "cpp" or "c" or "cxx" or "cc" or "h" or "hpp" => Language.CPP,
            "php" => Language.PHP,
            "sql" => Language.SQL,
            "vb" or "vbnet" => Language.VB,
            "go" => Language.Go,
            "rs" or "rust" => Language.Rust,
            "sh" or "bash" => Language.Bash,
            "yaml" or "yml" => Language.YAML,
            "markdown" or "md" => Language.Markdown,
            _ => Language.Unknown
        };
    }

    private List<Run> ParseCodeWithSyntaxHighlighting(string code, Language language)
    {
        var runs = new List<Run>();
        var patterns = GetSyntaxPatterns(language);
        
        var allMatches = new List<SyntaxMatch>();
        
        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(code, pattern.Pattern, RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                allMatches.Add(new SyntaxMatch
                {
                    Start = match.Index,
                    End = match.Index + match.Length,
                    Text = match.Value,
                    Type = pattern.Type,
                    Color = pattern.Color
                });
            }
        }

        // Sort matches by start position
        allMatches = allMatches.OrderBy(m => m.Start).ToList();

        // Remove overlapping matches (keep the first one)
        var filteredMatches = new List<SyntaxMatch>();
        int lastEnd = 0;
        
        foreach (var match in allMatches)
        {
            if (match.Start >= lastEnd)
            {
                filteredMatches.Add(match);
                lastEnd = match.End;
            }
        }

        // Create runs for each match and gaps
        int currentPos = 0;
        
        foreach (var match in filteredMatches)
        {
            // Add plain text before the match
            if (match.Start > currentPos)
            {
                var plainText = code.Substring(currentPos, match.Start - currentPos);
                runs.Add(new Run(plainText) { Foreground = new SolidColorBrush(Color.FromRgb(248, 248, 248)) });
            }
            
            // Add the highlighted match
            runs.Add(new Run(match.Text) { Foreground = new SolidColorBrush(match.Color) });
            currentPos = match.End;
        }
        
        // Add remaining plain text
        if (currentPos < code.Length)
        {
            var remainingText = code.Substring(currentPos);
            runs.Add(new Run(remainingText) { Foreground = new SolidColorBrush(Color.FromRgb(248, 248, 248)) });
        }

        return runs;
    }

    private List<SyntaxPattern> GetSyntaxPatterns(Language language)
    {
        return language switch
        {
            Language.CSharp => GetCSharpPatterns(),
            Language.Java => GetJavaPatterns(),
            Language.Python => GetPythonPatterns(),
            Language.JavaScript => GetJavaScriptPatterns(),
            Language.TypeScript => GetTypeScriptPatterns(),
            Language.HTML => GetHTMLPatterns(),
            Language.CSS => GetCSSPatterns(),
            Language.JSON => GetJSONPatterns(),
            Language.XML => GetXMLPatterns(),
            Language.CPP => GetCPPPatterns(),
            Language.PHP => GetPHPPatterns(),
            Language.SQL => GetSQLPatterns(),
            Language.VB => GetVBPatterns(),
            Language.Go => GetGoPatterns(),
            Language.Rust => GetRustPatterns(),
            Language.Bash => GetBashPatterns(),
            Language.YAML => GetYAMLPatterns(),
            Language.Markdown => GetMarkdownPatterns(),
            _ => new List<SyntaxPattern>()
        };
    }

    private List<SyntaxPattern> GetCSharpPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Comments
            new SyntaxPattern(@"//.*$", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            new SyntaxPattern(@"/\*[\s\S]*?\*/", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            
            // Strings
            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            
            // Keywords
            new SyntaxPattern(@"\b(abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|virtual|void|volatile|while)\b", SyntaxType.Keyword, Color.FromRgb(86, 156, 214)),
            
            // Numbers
            new SyntaxPattern(@"\b\d+(\.\d+)?[fFdDmM]?\b", SyntaxType.Number, Color.FromRgb(181, 206, 168)),
            
            // Types
            new SyntaxPattern(@"\b[A-Z][a-zA-Z0-9_]*\b", SyntaxType.Type, Color.FromRgb(78, 201, 176))
        };
    }

    private List<SyntaxPattern> GetJavaPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Comments
            new SyntaxPattern(@"//.*$", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            new SyntaxPattern(@"/\*[\s\S]*?\*/", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            
            // Strings
            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            
            // Keywords
            new SyntaxPattern(@"\b(abstract|assert|boolean|break|byte|case|catch|char|class|const|continue|default|do|double|else|enum|extends|final|finally|float|for|goto|if|implements|import|instanceof|int|interface|long|native|new|package|private|protected|public|return|short|static|strictfp|super|switch|synchronized|this|throw|throws|transient|try|void|volatile|while)\b", SyntaxType.Keyword, Color.FromRgb(86, 156, 214)),
            
            // Numbers
            new SyntaxPattern(@"\b\d+(\.\d+)?[fFdDlL]?\b", SyntaxType.Number, Color.FromRgb(181, 206, 168)),
            
            // Types
            new SyntaxPattern(@"\b[A-Z][a-zA-Z0-9_]*\b", SyntaxType.Type, Color.FromRgb(78, 201, 176))
        };
    }

    private List<SyntaxPattern> GetPythonPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Comments
            new SyntaxPattern(@"#.*$", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            
            // Strings
            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            new SyntaxPattern(@"'''(?:[^']|'(?!''))*'''", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            new SyntaxPattern(@""""".*?""""", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            
            // Keywords
            new SyntaxPattern(@"\b(and|as|assert|break|class|continue|def|del|elif|else|except|exec|finally|for|from|global|if|import|in|is|lambda|not|or|pass|print|raise|return|try|while|with|yield)\b", SyntaxType.Keyword, Color.FromRgb(86, 156, 214)),
            
            // Numbers
            new SyntaxPattern(@"\b\d+(\.\d+)?[eE][+-]?\d+[jJ]?\b|\b\d+\.\d+[jJ]?\b|\b\d+[jJ]\b", SyntaxType.Number, Color.FromRgb(181, 206, 168)),
            
            // Built-in functions
            new SyntaxPattern(@"\b(abs|all|any|bin|bool|chr|dict|dir|divmod|enumerate|eval|file|filter|float|format|frozenset|getattr|hasattr|hash|help|hex|id|input|int|isinstance|issubclass|iter|len|list|locals|map|max|min|next|oct|open|ord|pow|print|property|range|raw_input|reduce|reload|repr|reversed|round|set|setattr|slice|sorted|str|sum|super|tuple|type|unichr|unicode|vars|zip|__import__)\b", SyntaxType.Function, Color.FromRgb(220, 220, 170))
        };
    }

    private List<SyntaxPattern> GetJavaScriptPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Comments
            new SyntaxPattern(@"//.*$", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            new SyntaxPattern(@"/\*[\s\S]*?\*/", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            
            // Strings
            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            new SyntaxPattern(@"`(?:[^`\\]|\\.)*`", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            
            // Keywords
            new SyntaxPattern(@"\b(break|case|catch|class|const|continue|debugger|default|delete|do|else|export|extends|finally|for|function|if|import|in|instanceof|let|new|return|super|switch|this|throw|try|typeof|var|void|while|with|yield)\b", SyntaxType.Keyword, Color.FromRgb(86, 156, 214)),
            
            // Numbers
            new SyntaxPattern(@"\b\d+(\.\d+)?[eE][+-]?\d+[fFdD]?\b|\b0x[0-9a-fA-F]+\b|\b0b[01]+\b", SyntaxType.Number, Color.FromRgb(181, 206, 168)),
            
            // Functions
            new SyntaxPattern(@"\b\w+(?=\s*\()", SyntaxType.Function, Color.FromRgb(220, 220, 170))
        };
    }

    private List<SyntaxPattern> GetTypeScriptPatterns()
    {
        var patterns = GetJavaScriptPatterns();
        patterns.AddRange(new List<SyntaxPattern>
        {
            // TypeScript specific keywords
            new SyntaxPattern(@"\b(interface|type|enum|namespace|module|declare|abstract|implements|extends|public|private|protected|readonly|static|async|await)\b", SyntaxType.Keyword, Color.FromRgb(86, 156, 214)),
            
            // Type annotations
            new SyntaxPattern(@":\s*\w+(?:<[^>]*>)?", SyntaxType.Type, Color.FromRgb(78, 201, 176))
        });
        return patterns;
    }

    private List<SyntaxPattern> GetHTMLPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Comments
            new SyntaxPattern(@"<!--[\s\S]*?-->", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            
            // Tags
            new SyntaxPattern(@"</?[a-zA-Z][^>]*>", SyntaxType.Tag, Color.FromRgb(86, 156, 214)),
            
            // Attributes
            new SyntaxPattern(@"\b\w+\s*=", SyntaxType.Attribute, Color.FromRgb(156, 220, 254)),
            
            // Strings
            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", SyntaxType.String, Color.FromRgb(206, 145, 120))
        };
    }

    private List<SyntaxPattern> GetCSSPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Comments
            new SyntaxPattern(@"/\*[\s\S]*?\*/", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            
            // Selectors
            new SyntaxPattern(@"[.#]?[a-zA-Z-]+\s*{", SyntaxType.Selector, Color.FromRgb(86, 156, 214)),
            
            // Properties
            new SyntaxPattern(@"\b[a-zA-Z-]+\s*:", SyntaxType.Property, Color.FromRgb(156, 220, 254)),
            
            // Values
            new SyntaxPattern(@":\s*[^;]+", SyntaxType.Value, Color.FromRgb(206, 145, 120)),
            
            // Numbers
            new SyntaxPattern(@"\b\d+(\.\d+)?(px|em|rem|%|vh|vw|pt|pc|in|cm|mm)\b", SyntaxType.Number, Color.FromRgb(181, 206, 168))
        };
    }

    private List<SyntaxPattern> GetJSONPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Keys
            new SyntaxPattern(@"""[^""]*""\s*:", SyntaxType.Key, Color.FromRgb(156, 220, 254)),
            
            // Strings
            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            
            // Numbers
            new SyntaxPattern(@"\b\d+(\.\d+)?([eE][+-]?\d+)?\b", SyntaxType.Number, Color.FromRgb(181, 206, 168)),
            
            // Booleans and null
            new SyntaxPattern(@"\b(true|false|null)\b", SyntaxType.Keyword, Color.FromRgb(86, 156, 214))
        };
    }

    private List<SyntaxPattern> GetXMLPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Comments
            new SyntaxPattern(@"<!--[\s\S]*?-->", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            
            // Tags
            new SyntaxPattern(@"</?[a-zA-Z][^>]*>", SyntaxType.Tag, Color.FromRgb(86, 156, 214)),
            
            // Attributes
            new SyntaxPattern(@"\b\w+\s*=", SyntaxType.Attribute, Color.FromRgb(156, 220, 254)),
            
            // Strings
            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", SyntaxType.String, Color.FromRgb(206, 145, 120))
        };
    }

    private List<SyntaxPattern> GetCPPPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Comments
            new SyntaxPattern(@"//.*$", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            new SyntaxPattern(@"/\*[\s\S]*?\*/", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            
            // Strings
            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            
            // Keywords
            new SyntaxPattern(@"\b(alignas|alignof|and|and_eq|asm|auto|bitand|bitor|bool|break|case|catch|char|char16_t|char32_t|class|compl|const|constexpr|const_cast|continue|decltype|default|delete|do|double|dynamic_cast|else|enum|explicit|export|extern|false|float|for|friend|goto|if|inline|int|long|mutable|namespace|new|noexcept|not|not_eq|nullptr|operator|or|or_eq|private|protected|public|register|reinterpret_cast|return|short|signed|sizeof|static|static_assert|static_cast|struct|switch|template|this|thread_local|throw|true|try|typedef|typeid|typename|union|unsigned|using|virtual|void|volatile|wchar_t|while|xor|xor_eq)\b", SyntaxType.Keyword, Color.FromRgb(86, 156, 214)),
            
            // Numbers
            new SyntaxPattern(@"\b\d+(\.\d+)?[fFdDlLuU]?\b|\b0x[0-9a-fA-F]+\b|\b0b[01]+\b", SyntaxType.Number, Color.FromRgb(181, 206, 168)),
            
            // Types
            new SyntaxPattern(@"\b[A-Z][a-zA-Z0-9_]*\b", SyntaxType.Type, Color.FromRgb(78, 201, 176))
        };
    }

    private List<SyntaxPattern> GetPHPPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Comments
            new SyntaxPattern(@"//.*$", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            new SyntaxPattern(@"#.*$", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            new SyntaxPattern(@"/\*[\s\S]*?\*/", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            
            // Strings
            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            
            // Keywords
            new SyntaxPattern(@"\b(abstract|and|array|as|break|callable|case|catch|class|clone|const|continue|declare|default|die|do|echo|else|elseif|empty|enddeclare|endfor|endforeach|endif|endswitch|endwhile|eval|exit|extends|final|finally|for|foreach|function|global|goto|if|implements|include|include_once|instanceof|insteadof|interface|isset|list|namespace|new|or|print|private|protected|public|require|require_once|return|static|switch|throw|trait|try|unset|use|var|while|xor|yield)\b", SyntaxType.Keyword, Color.FromRgb(86, 156, 214)),
            
            // Variables
            new SyntaxPattern(@"\$[a-zA-Z_][a-zA-Z0-9_]*", SyntaxType.Variable, Color.FromRgb(156, 220, 254)),
            
            // Numbers
            new SyntaxPattern(@"\b\d+(\.\d+)?\b", SyntaxType.Number, Color.FromRgb(181, 206, 168))
        };
    }

    private List<SyntaxPattern> GetSQLPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Comments
            new SyntaxPattern(@"--.*$", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            new SyntaxPattern(@"/\*[\s\S]*?\*/", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            
            // Keywords
            new SyntaxPattern(@"\b(SELECT|FROM|WHERE|INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|TABLE|INDEX|VIEW|PROCEDURE|FUNCTION|TRIGGER|DATABASE|SCHEMA|GRANT|REVOKE|COMMIT|ROLLBACK|BEGIN|END|IF|ELSE|WHILE|FOR|CURSOR|DECLARE|SET|EXEC|EXECUTE|UNION|JOIN|INNER|LEFT|RIGHT|OUTER|ON|GROUP|BY|ORDER|HAVING|DISTINCT|TOP|LIMIT|OFFSET|CASE|WHEN|THEN|ELSE|END|AS|IN|EXISTS|BETWEEN|LIKE|IS|NULL|NOT|AND|OR|TRUE|FALSE)\b", SyntaxType.Keyword, Color.FromRgb(86, 156, 214)),
            
            // Strings
            new SyntaxPattern(@"'[^']*'", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            
            // Numbers
            new SyntaxPattern(@"\b\d+(\.\d+)?\b", SyntaxType.Number, Color.FromRgb(181, 206, 168))
        };
    }

    private List<SyntaxPattern> GetVBPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Comments
            new SyntaxPattern(@"'.*$", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            
            // Strings
            new SyntaxPattern(@"""(?:[^""]|"""")*""", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            
            // Keywords
            new SyntaxPattern(@"\b(AddHandler|AddressOf|Alias|And|AndAlso|As|Boolean|ByRef|Byte|ByVal|Call|Case|Catch|CBool|CByte|CChar|CDate|CDbl|CDec|CInt|CLng|CObj|Continue|CSByte|CShort|CSng|CStr|CType|CUInt|CULng|CUShort|Dim|Do|Each|Else|ElseIf|End|EndIf|Enum|Erase|Error|Event|Exit|False|Finally|For|Friend|Function|Get|GetType|GetXMLNamespace|Global|GoTo|Handles|If|Implements|Imports|In|Inherits|Integer|Interface|Is|IsNot|Let|Lib|Like|Long|Loop|Me|Mod|Module|MustInherit|MustOverride|MyBase|MyClass|Namespace|Narrowing|Next|Not|Nothing|NotInheritable|NotOverridable|Object|Of|On|Operator|Option|Optional|Or|OrElse|Overloads|Overridable|Overrides|ParamArray|Partial|Private|Property|Protected|Public|RaiseEvent|ReadOnly|ReDim|REM|RemoveHandler|Resume|Return|SByte|Select|Set|Shadows|Shared|Short|Single|Static|Step|Stop|String|Structure|Sub|SyncLock|Then|Throw|To|True|Try|TryCast|TypeOf|UInteger|ULong|UShort|Using|When|While|Widening|With|WithEvents|WriteOnly|Xor)\b", SyntaxType.Keyword, Color.FromRgb(86, 156, 214)),
            
            // Numbers
            new SyntaxPattern(@"\b\d+(\.\d+)?[fFdDlL]?\b", SyntaxType.Number, Color.FromRgb(181, 206, 168))
        };
    }

    private List<SyntaxPattern> GetGoPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Comments
            new SyntaxPattern(@"//.*$", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            new SyntaxPattern(@"/\*[\s\S]*?\*/", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            
            // Strings
            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            new SyntaxPattern(@"`(?:[^`\\]|\\.)*`", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            
            // Keywords
            new SyntaxPattern(@"\b(break|case|chan|const|continue|default|defer|else|fallthrough|for|func|go|goto|if|import|interface|map|package|range|return|select|struct|switch|type|var)\b", SyntaxType.Keyword, Color.FromRgb(86, 156, 214)),
            
            // Numbers
            new SyntaxPattern(@"\b\d+(\.\d+)?[eE][+-]?\d+[fF]?\b|\b0x[0-9a-fA-F]+\b|\b0b[01]+\b", SyntaxType.Number, Color.FromRgb(181, 206, 168)),
            
            // Types
            new SyntaxPattern(@"\b[A-Z][a-zA-Z0-9_]*\b", SyntaxType.Type, Color.FromRgb(78, 201, 176))
        };
    }

    private List<SyntaxPattern> GetRustPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Comments
            new SyntaxPattern(@"//.*$", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            new SyntaxPattern(@"/\*[\s\S]*?\*/", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            
            // Strings
            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            new SyntaxPattern(@"r#*""(?:[^""\\]|\\.)*""#*", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            
            // Keywords
            new SyntaxPattern(@"\b(as|async|await|break|const|continue|crate|dyn|else|enum|extern|false|fn|for|if|impl|in|let|loop|match|mod|move|mut|pub|ref|return|self|Self|static|struct|super|trait|true|type|union|unsafe|use|where|while|yield)\b", SyntaxType.Keyword, Color.FromRgb(86, 156, 214)),
            
            // Numbers
            new SyntaxPattern(@"\b\d+(\.\d+)?[eE][+-]?\d+[fF]?\b|\b0x[0-9a-fA-F]+\b|\b0b[01]+\b|\b0o[0-7]+\b", SyntaxType.Number, Color.FromRgb(181, 206, 168)),
            
            // Types
            new SyntaxPattern(@"\b[A-Z][a-zA-Z0-9_]*\b", SyntaxType.Type, Color.FromRgb(78, 201, 176))
        };
    }

    private List<SyntaxPattern> GetBashPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Comments
            new SyntaxPattern(@"#.*$", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            
            // Strings
            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            
            // Keywords
            new SyntaxPattern(@"\b(if|then|else|elif|fi|case|esac|for|while|do|done|in|function|local|return|break|continue|exit|trap|readonly|declare|typeset|export|unset|alias|unalias|set|unset|shift|getopts|eval|exec|source|dot|cd|pwd|pushd|popd|dirs|let|test|\[|\[\[|time|times|trap|type|ulimit|umask|wait|command|builtin|enable|hash|help|history|jobs|kill|disown|fg|bg|suspend|logout|exit)\b", SyntaxType.Keyword, Color.FromRgb(86, 156, 214)),
            
            // Variables
            new SyntaxPattern(@"\$[a-zA-Z_][a-zA-Z0-9_]*", SyntaxType.Variable, Color.FromRgb(156, 220, 254)),
            
            // Numbers
            new SyntaxPattern(@"\b\d+\b", SyntaxType.Number, Color.FromRgb(181, 206, 168))
        };
    }

    private List<SyntaxPattern> GetYAMLPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Comments
            new SyntaxPattern(@"#.*$", SyntaxType.Comment, Color.FromRgb(106, 153, 85)),
            
            // Keys
            new SyntaxPattern(@"^[\s]*[a-zA-Z_][a-zA-Z0-9_]*\s*:", SyntaxType.Key, Color.FromRgb(156, 220, 254)),
            
            // Strings
            new SyntaxPattern(@"""(?:[^""\\]|\\.)*""", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            new SyntaxPattern(@"'(?:[^'\\]|\\.)*'", SyntaxType.String, Color.FromRgb(206, 145, 120)),
            
            // Booleans and null
            new SyntaxPattern(@"\b(true|false|null|yes|no|on|off)\b", SyntaxType.Keyword, Color.FromRgb(86, 156, 214)),
            
            // Numbers
            new SyntaxPattern(@"\b\d+(\.\d+)?([eE][+-]?\d+)?\b", SyntaxType.Number, Color.FromRgb(181, 206, 168))
        };
    }

    private List<SyntaxPattern> GetMarkdownPatterns()
    {
        return new List<SyntaxPattern>
        {
            // Headers
            new SyntaxPattern(@"^#{1,6}\s+.*$", SyntaxType.Header, Color.FromRgb(86, 156, 214)),
            
            // Bold and italic
            new SyntaxPattern(@"\*\*[^*]+\*\*", SyntaxType.Bold, Color.FromRgb(220, 220, 170)),
            new SyntaxPattern(@"\*[^*]+\*", SyntaxType.Italic, Color.FromRgb(220, 220, 170)),
            
            // Code blocks
            new SyntaxPattern(@"```[\s\S]*?```", SyntaxType.CodeBlock, Color.FromRgb(106, 153, 85)),
            new SyntaxPattern(@"`[^`]+`", SyntaxType.InlineCode, Color.FromRgb(106, 153, 85)),
            
            // Links
            new SyntaxPattern(@"\[[^\]]+\]\([^)]+\)", SyntaxType.Link, Color.FromRgb(78, 201, 176)),
            
            // Lists
            new SyntaxPattern(@"^\s*[-*+]\s+", SyntaxType.ListItem, Color.FromRgb(181, 206, 168)),
            new SyntaxPattern(@"^\s*\d+\.\s+", SyntaxType.ListItem, Color.FromRgb(181, 206, 168))
        };
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _textBlock = null;
        base.OnDetachedFromVisualTree(e);
    }
}

// Supporting classes for syntax highlighting
public enum Language
{
    Unknown,
    CSharp,
    Java,
    Python,
    JavaScript,
    TypeScript,
    HTML,
    CSS,
    JSON,
    XML,
    CPP,
    PHP,
    SQL,
    VB,
    Go,
    Rust,
    Bash,
    YAML,
    Markdown
}

public enum SyntaxType
{
    Keyword,
    String,
    Comment,
    Number,
    Type,
    Function,
    Variable,
    Tag,
    Attribute,
    Selector,
    Property,
    Value,
    Key,
    Header,
    Bold,
    Italic,
    CodeBlock,
    InlineCode,
    Link,
    ListItem
}

public class SyntaxPattern
{
    public string Pattern { get; set; }
    public SyntaxType Type { get; set; }
    public Color Color { get; set; }

    public SyntaxPattern(string pattern, SyntaxType type, Color color)
    {
        Pattern = pattern;
        Type = type;
        Color = color;
    }
}

public class SyntaxMatch
{
    public int Start { get; set; }
    public int End { get; set; }
    public string Text { get; set; } = string.Empty;
    public SyntaxType Type { get; set; }
    public Color Color { get; set; }
}