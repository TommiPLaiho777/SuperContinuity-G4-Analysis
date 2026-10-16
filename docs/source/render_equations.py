"""Render the handbook's own LaTeX equations; requires matplotlib, no TeX install."""
import json
import re
import sys
from pathlib import Path

from matplotlib import mathtext


def main():
    manifest = Path(sys.argv[1])
    equations = json.loads(manifest.read_text(encoding="utf-8"))
    for entry in equations:
        # Mathtext supports the long relation aliases and scalable delimiters;
        # normalize equivalent LaTeX spellings without changing the mathematics.
        latex = re.sub(r"\\(le|ge)(?![A-Za-z])", lambda m: "\\" + m.group(1) + "q", entry["latex"])
        latex = latex.replace(r"\bigl", r"\left").replace(r"\bigr", r"\right")
        mathtext.math_to_image(
            "$" + latex + "$",
            entry["path"], dpi=300, format="png", color="#15334c",
        )


if __name__ == "__main__":
    main()
