"""The C# identifier rules that a token's name and a query's name meet.

Mirrors `SqlIdentifier.cs` in `src/SqlSource/`, and the methods of Roslyn's `SyntaxFacts` that it and the token
scanner call: `IsIdentifierStartCharacter`, `IsIdentifierPartCharacter`, `IsValidIdentifier` and `GetKeywordKind`, as
Microsoft.CodeAnalysis.CSharp 4.14.0 has them.

Roslyn tests UTF-16 code units, as `text.py` explains, so a code point outside the BMP is a surrogate here: no part of
an identifier.
"""

from sqlfluff_templater_sqlsource.text import category

# The categories of a letter to Roslyn: a letter number counts.
_LETTER_CATEGORIES = frozenset(("Lu", "Ll", "Lt", "Lm", "Lo", "Nl"))

# What else may follow the first character: a decimal digit, a connector, a combining mark and a format character.
_PART_CATEGORIES = _LETTER_CATEGORIES | {"Nd", "Pc", "Mn", "Mc", "Cf"}

# The words that SyntaxFacts.GetKeywordKind gives a kind: the reserved keywords, and Roslyn's four undocumented ones.
# A contextual keyword such as `where` is none.
# fmt: off
_RESERVED_KEYWORDS = frozenset((
    "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
    "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
    "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
    "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
    "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
    "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
    "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while", "__arglist", "__makeref",
    "__reftype", "__refvalue",
))
# fmt: on


def is_identifier_start_character(value: str) -> bool:
    """As `SyntaxFacts.IsIdentifierStartCharacter`: an ASCII letter, `_`, or a letter above U+007F."""
    if value.isascii():
        return value.isalpha() or value == "_"
    return category(value) in _LETTER_CATEGORIES


def is_identifier_part_character(value: str) -> bool:
    """As `SyntaxFacts.IsIdentifierPartCharacter`: an ASCII letter or digit, `_`, or above U+007F a letter, a decimal
    digit, a connector, a combining mark or a format character."""
    if value.isascii():
        return value.isalnum() or value == "_"
    return category(value) in _PART_CATEGORIES


def is_valid(value: str) -> bool:
    """True when `value` has the form of an identifier.  Keywords pass.  A format character such as a zero-width space
    does not: C# allows one in an identifier and then ignores it, so two names that differ only by one would collide
    in generated code."""
    return (
        value != ""
        and is_identifier_start_character(value[0])
        and all(is_identifier_part_character(character) for character in value[1:])
        and not any(category(character) == "Cf" for character in value)
    )


def is_reserved_keyword(value: str) -> bool:
    """True for a reserved keyword such as `class`.  A contextual keyword such as `where` is not one."""
    return value in _RESERVED_KEYWORDS


def is_usable_name(value: str) -> bool:
    """True when `value` can be the name of a generated member or parameter."""
    return is_valid(value) and not is_reserved_keyword(value)
