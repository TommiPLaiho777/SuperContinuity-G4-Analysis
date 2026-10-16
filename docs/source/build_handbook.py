"""Reproducible handbook build: reportlab plus a matplotlib-capable Python.

Run from the release root with a Python that supplies reportlab and Pillow:
  python docs/source/build_handbook.py --math-python <python-with-matplotlib>
The PDF is written to output/pdf and copied to docs for release packaging.
No source-code evaluation, Rhino launch, network access, or user model access occurs.
"""
import argparse
import hashlib
import html
import json
import re
import shutil
import subprocess
from pathlib import Path

from PIL import Image as PillowImage
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, PageBreak, Image, Table, TableStyle,
    KeepTogether,
)


def inline(text):
    text = html.escape(text)
    text = re.sub(r"`([^`]+)`", r'<font name="Courier" size="8.4">\1</font>', text)
    return re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", text)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--math-python", default="python")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    source = Path(__file__).with_name("G4_Mathematics.md")
    content = source.read_text(encoding="utf-8")
    pages = content.strip().split("\n---\n")
    tmp = root / "tmp/pdfs/handbook"
    tmp.mkdir(parents=True, exist_ok=True)
    equations = []
    for match in re.finditer(r"\$\$(.*?)\$\$", content, re.S):
        latex = match.group(1).strip()
        if any(x["latex"] == latex for x in equations):
            continue
        equations.append({"latex": latex, "path": str(tmp / f"eq{len(equations):03d}.png")})
    manifest = tmp / "equations.json"
    manifest.write_text(json.dumps(equations), encoding="utf-8")
    subprocess.run([args.math_python, str(Path(__file__).with_name("render_equations.py")), str(manifest)], check=True)
    equation_files = {x["latex"]: Path(x["path"]) for x in equations}
    styles = getSampleStyleSheet()
    body = ParagraphStyle("BodySC", fontName="Helvetica", fontSize=10.3, leading=15.5,
                          textColor=colors.HexColor("#263747"), spaceAfter=10)
    title = ParagraphStyle("TitleSC", parent=body, fontName="Helvetica-Bold", fontSize=23,
                           leading=28, textColor=colors.HexColor("#15334c"), spaceAfter=19)
    subtitle = ParagraphStyle("SubSC", parent=body, fontName="Helvetica-Bold", fontSize=13,
                              leading=18, textColor=colors.HexColor("#27768b"), spaceAfter=15)
    note = ParagraphStyle("NoteSC", parent=body, fontSize=10.1, leading=15,
                          backColor=colors.HexColor("#ecf3f6"), borderPadding=10,
                          spaceBefore=6, spaceAfter=15)
    cell = ParagraphStyle("CellSC", parent=body, fontSize=8.5, leading=12, spaceAfter=0)
    width = A4[0] - 108
    story = []
    for page_index, page in enumerate(pages):
        if page_index:
            story.append(PageBreak())
        blocks = re.split(r"\n\s*\n", page.strip())
        for block in blocks:
            block = block.strip()
            if not block:
                continue
            if block.startswith("# "):
                story.append(Paragraph(inline(block[2:]), title))
            elif block.startswith("## "):
                story.append(Paragraph(inline(block[3:]), subtitle))
            elif re.fullmatch(r"!\[([^\]]*)\]\(([^)]+)\)", block):
                match = re.fullmatch(r"!\[([^\]]*)\]\(([^)]+)\)", block)
                path = (source.parent / match.group(2)).resolve()
                with PillowImage.open(path) as img:
                    w, h = img.size
                scale = min(width / w, 230 / h)
                story.extend([Image(str(path), width=w*scale, height=h*scale), Spacer(1, 12)])
            elif block.startswith("$$"):
                # An authoring block may contain more than one displayed equation.
                for eq in re.findall(r"\$\$(.*?)\$\$", block, re.S):
                    path = equation_files[eq.strip()]
                    with PillowImage.open(path) as img:
                        w, h = img.size
                    scale = min(72 / 300 * 1.35, width / w)
                    story.append(KeepTogether([Spacer(1, 5), Image(str(path), width=w*scale, height=h*scale), Spacer(1, 12)]))
            elif block.startswith("| "):
                rows = [[Paragraph(inline(x.strip()), cell) for x in line.strip().strip("|").split("|")]
                        for line in block.splitlines() if not re.match(r"^\|[\s:|\-]+\|$", line.strip())]
                columns = len(rows[0])
                if columns == 4:
                    widths = [width*.15, width*.48, width*.10, width*.27]
                elif columns == 3:
                    widths = [width*.27, width*.32, width*.41]
                else:
                    widths = [width*.31, width*.69]
                table = Table(rows, colWidths=widths, hAlign="LEFT", repeatRows=1)
                table.setStyle(TableStyle([
                    ("BACKGROUND",(0,0),(-1,0),colors.HexColor("#e1edf2")),
                    ("ROWBACKGROUNDS",(0,1),(-1,-1),[colors.white,colors.HexColor("#f5f8fa")]),
                    ("VALIGN",(0,0),(-1,-1),"TOP"),
                    ("TOPPADDING",(0,0),(-1,-1),7),
                    ("BOTTOMPADDING",(0,0),(-1,-1),7),
                    ("LINEBELOW",(0,0),(-1,0),.7,colors.HexColor("#9db6c5")),
                ]))
                story.extend([table, Spacer(1, 12)])
            elif block.startswith("> "):
                story.append(Paragraph(inline(block[2:].replace("\n", " ")), note))
            elif block.startswith("https://"):
                url = block.strip()
                story.append(Paragraph('<link href="'+html.escape(url)+'" color="#27768b">'+html.escape(url)+'</link>', cell))
                story.append(Spacer(1, 10))
            else:
                story.append(Paragraph(inline(block.replace("\n", " ")), body))
    out = root / "output/pdf/SuperContinuity_G4_Mathematics.pdf"
    out.parent.mkdir(parents=True, exist_ok=True)
    def chrome(canvas, doc):
        canvas.saveState()
        canvas.setStrokeColor(colors.HexColor("#b8cbd5"))
        canvas.line(54, A4[1]-35, A4[0]-54, A4[1]-35)
        canvas.setFont("Helvetica", 8)
        canvas.setFillColor(colors.HexColor("#537083"))
        canvas.drawString(54,A4[1]-26,"SUPERCONTINUITY / G4 ANALYSIS ALPHA 0.1.1")
        canvas.drawString(54,30,"Mathematical method and evidence limits / MIT / Tommi P. Laiho")
        canvas.drawRightString(A4[0]-54,30,str(doc.page))
        canvas.restoreState()
    document = SimpleDocTemplate(str(out), pagesize=A4, leftMargin=54, rightMargin=54,
                                topMargin=55, bottomMargin=53,
                                title="SuperContinuity G4 Mathematics - A Guided Explanation",
                                author="Tommi P. Laiho", subject="Sampled geometric G4 analysis, derivations, and implementation limits")
    document.build(story, onFirstPage=chrome, onLaterPages=chrome)
    from pypdf import PdfReader
    count = len(PdfReader(out).pages)
    if count != len(pages):
        raise RuntimeError(f"Layout overflow: expected {len(pages)} pages, produced {count}; inspect and repair.")
    target = root / "docs/SuperContinuity_G4_Mathematics.pdf"
    shutil.copyfile(out,target)
    print(json.dumps({"pdf":str(target),"pages":count,"equations":len(equations),"sha256":hashlib.sha256(target.read_bytes()).hexdigest().upper()}))


if __name__ == "__main__":
    main()
