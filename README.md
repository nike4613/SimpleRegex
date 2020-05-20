# Simple Regular Expressions

This is a simple, bytecode-interpreter based implementation of a regex engine,
written in C#.

It supports:
- Ranged character groups (`[a-z]`)
- Inverted character groups (`[^a-z]`)
- Builtin character groups `\s`, `\S`, `\w`, `\W`, `\d`, and `\D`
- Builtin character groups within range character groups (`[\s]`)
- 'Any' character group (`.`)
- The beginning and end of string anchors (`^` and `$`)
- Capture groups `(...)`
- Non-capturing groups `(?:...)`
- Alternations (`a|b`)
- The standard quantifiers `?`, `*`, and `+`
- The lazy versions of the above

This is enough for most useful regexes that you will find. For example, the
[Semver](https://semver.org) recommended regex for parsing semantic versions
uses only these features:
```
^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$
```

This is even enough to support a whitespace-stripping regex, because `\s` is implemented using `char.IsWhitespace`:
```
^[\s]*(.*?)[\s]*$
```

## How do I use it?

Please don't. I *promise* it performs far worse that .NET's built-in regex
engine on every single platform. I would be *very* surprised if it outperformed
about any implementation. 

On top of that, it has fewer features than `System.Text.RegularExpressions.Regex`,
and so there is literally zero reason to use this as anything other than a
reference for if you are curious how such an engine might work, and/or you
want to write your own.

**There will be no active maintenance.**
