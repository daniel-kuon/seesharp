"""Split the book PDF into chunks small enough for Google Translate, and merge the translated chunks back.

    python book_pdf.py split csharp14.pdf translation/de        -> csharp14-01.pdf, -02.pdf ... (< 9.5 MB, <= 300 pages each)
    python book_pdf.py merge translation/en csharp14.en.pdf     -> one PDF from the chunks in name order

Chunks never cut a page and keep their order, so the merged translation has the same physical page numbers as the
original (printed page + 32) and SeeSharp's book links keep working. Needs `pip install pypdf`.
"""
import io
import sys
from pathlib import Path

from pypdf import PdfReader, PdfWriter

MAX_BYTES = 9_500_000  # Google Translate accepts 10 MB; leave headroom
MAX_PAGES = 300        # stay below common page limits for document translation


def size_of(reader: PdfReader, start: int, end: int) -> int:
    writer = PdfWriter()
    for i in range(start, end):
        writer.add_page(reader.pages[i])
    writer.compress_identical_objects()
    buffer = io.BytesIO()
    writer.write(buffer)
    return buffer.tell()


def split(source: Path, out_dir: Path) -> None:
    reader = PdfReader(source)
    out_dir.mkdir(parents=True, exist_ok=True)
    total, start, part = len(reader.pages), 0, 1
    while start < total:
        # Largest end (exclusive) that fits: grow fast while small, then binary-search the boundary.
        lo, hi = start + 1, min(total, start + MAX_PAGES)
        if size_of(reader, start, lo) > MAX_BYTES:
            raise SystemExit(f"page {start + 1} alone is larger than {MAX_BYTES} bytes")
        while lo < hi:
            mid = (lo + hi + 1) // 2
            if size_of(reader, start, mid) <= MAX_BYTES:
                lo = mid
            else:
                hi = mid - 1
        end = lo
        writer = PdfWriter()
        for i in range(start, end):
            writer.add_page(reader.pages[i])
        writer.compress_identical_objects()
        target = out_dir / f"{source.stem}-{part:02d}.pdf"
        with target.open("wb") as f:
            writer.write(f)
        print(f"{target.name}: physical pages {start + 1}-{end} ({end - start} pages, {target.stat().st_size / 1e6:.1f} MB)")
        start, part = end, part + 1


def merge(in_dir: Path, target: Path) -> None:
    writer = PdfWriter()
    chunks = sorted(in_dir.glob("*.pdf"))
    for chunk in chunks:
        writer.append(PdfReader(chunk))
    with target.open("wb") as f:
        writer.write(f)
    print(f"{target}: {len(writer.pages)} pages from {len(chunks)} chunks")


if __name__ == "__main__":
    command, a, b = sys.argv[1:4]
    (split if command == "split" else merge)(Path(a), Path(b))
