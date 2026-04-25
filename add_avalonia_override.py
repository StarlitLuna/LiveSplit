"""Bulk-add `GetSettingsControlAvalonia` override to component classes.

For each component .cs file passed on argv, find the existing
`public Control GetSettingsControl(LayoutMode mode)` method and inject an
`Avalonia.Controls.Control GetSettingsControlAvalonia(LayoutMode mode)` method
right after it. The new method delegates to AvaloniaSettingsBuilder.Build using
the same Settings object the WinForms version returns.
"""
import re
import sys
from pathlib import Path


METHOD_RE = re.compile(
    r"(?P<indent>[ \t]+)public\s+(?:override\s+|virtual\s+|new\s+)?Control\s+GetSettingsControl\s*\(\s*LayoutMode\s+mode\s*\)\s*\{"
    r"(?P<body>.*?)\r?\n(?P=indent)\}",
    re.DOTALL,
)


def find_settings_target(body: str) -> str | None:
    """From the WinForms GetSettingsControl body, extract what it returns.

    Common patterns:
        return Settings;
        Settings.Mode = mode; return Settings;
        return InternalComponent.GetSettingsControl(mode);
        return null;
    Returns the C# expression to use as the Avalonia builder's target, or None.
    """
    # First, look for `return X;` lines
    m = re.search(r"return\s+(.+?);", body, re.DOTALL)
    if not m:
        return None
    expr = m.group(1).strip()
    # Strip stray newlines/whitespace
    expr = re.sub(r"\s+", " ", expr)
    return expr


def transform(text: str, component_name: str) -> tuple[str, bool]:
    m = METHOD_RE.search(text)
    if not m:
        return text, False

    indent = m.group("indent")
    body = m.group("body")
    target = find_settings_target(body)
    if target is None:
        return text, False

    # If it returns null (component has no settings), Avalonia version also returns null.
    if target == "null":
        avalonia_method = (
            f"\n\n{indent}public Avalonia.Controls.Control GetSettingsControlAvalonia(LayoutMode mode) => null;"
        )
    elif "GetSettingsControl(mode)" in target:
        # Composite: forward to inner GetSettingsControlAvalonia.
        forwarded = target.replace("GetSettingsControl(mode)", "GetSettingsControlAvalonia(mode)")
        avalonia_method = (
            f"\n\n{indent}public Avalonia.Controls.Control GetSettingsControlAvalonia(LayoutMode mode) => {forwarded};"
        )
    else:
        # Settings object — feed it to the auto-builder.
        # If body sets Settings.Mode = mode, replicate that.
        prefix = ""
        if "Settings.Mode = mode" in body:
            prefix = f"{indent}    Settings.Mode = mode;\n"
        avalonia_method = (
            f"\n\n{indent}public Avalonia.Controls.Control GetSettingsControlAvalonia(LayoutMode mode)\n"
            f"{indent}{{\n"
            f"{prefix}"
            f"{indent}    return LiveSplit.UI.AvaloniaSettingsBuilder.Build({target}, {component_name});\n"
            f"{indent}}}"
        )

    insert_at = m.end()
    new_text = text[:insert_at] + avalonia_method + text[insert_at:]
    return new_text, True


def derive_component_name_expr(text: str) -> str:
    """Try to parse `public string ComponentName => ...` and reuse the same expression."""
    m = re.search(r"public\s+string\s+ComponentName\s*=>\s*([^;]+);", text)
    if m:
        return m.group(1).strip()
    m = re.search(r"public\s+string\s+ComponentName\s*\{\s*get\s*\{\s*return\s+([^;]+);", text)
    if m:
        return m.group(1).strip()
    return '"Component"'


def main() -> int:
    changed = 0
    skipped = 0
    for arg in sys.argv[1:]:
        path = Path(arg)
        if not path.is_file():
            print(f"missing: {path}", file=sys.stderr)
            continue
        original = path.read_text(encoding="utf-8")
        if "GetSettingsControlAvalonia" in original:
            print(f"already-migrated: {path}")
            skipped += 1
            continue
        component_name = derive_component_name_expr(original)
        updated, ok = transform(original, component_name)
        if ok:
            path.write_text(updated, encoding="utf-8")
            print(f"injected: {path}")
            changed += 1
        else:
            print(f"no-match: {path}")
            skipped += 1
    print(f"\nDone. injected={changed} skipped={skipped}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
