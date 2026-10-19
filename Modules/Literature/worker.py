"""Offline PDF rendering and English-Chinese inference. Never imports an HTTP client."""
import collections
import hashlib
import importlib.metadata
import json
import os
from pathlib import Path
import re
import socket
import sys
from datetime import datetime, timezone


def no_network(*args, **kwargs):
    raise RuntimeError("Network access is disabled in the literature worker")


sys.stdout.reconfigure(encoding="utf-8")
sys.stderr.reconfigure(encoding="utf-8")


def emit(value):
    print(json.dumps(value, ensure_ascii=False), flush=True)


def save(path, document):
    target = Path(path)
    temporary = target.with_suffix(".writing")
    document["UpdatedAt"] = datetime.now(timezone.utc).isoformat()
    temporary.write_text(json.dumps(document, ensure_ascii=False), encoding="utf-8")
    os.replace(temporary, target)


def fingerprint(path):
    digest = hashlib.sha256()
    with open(path, "rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def open_pdf(path):
    import pypdfium2 as pdfium
    file = Path(path)
    if not file.is_file() or file.stat().st_size > 200 * 1024 * 1024:
        raise ValueError("文献不存在或超过 200 MB，请选择较小的本地 PDF。")
    pdf = pdfium.PdfDocument(str(file))
    if len(pdf) < 1 or len(pdf) > 1000:
        pdf.close()
        raise ValueError("PDF 页数须为 1 至 1000 页。")
    return pdf


def inspect(source, output):
    pdf = open_pdf(source)
    try:
        pages = []
        total_text = 0
        for index in range(len(pdf)):
            page = pdf[index]
            textpage = page.get_textpage()
            text = textpage.get_text_range().replace("\r\n", "\n").replace("\r", "\n")
            textpage.close()
            page.close()
            total_text += len(text)
            if total_text > 5_000_000 or len(text) > 250_000:
                raise ValueError("提取文本过大，请拆分文献后处理。")
            pages.append({"Number": index + 1, "OriginalText": text, "ChineseText": "", "Status": "未翻译" if text.strip() else "无可提取文本（不支持扫描 OCR）", "NumericReview": False})
            emit({"Kind": "progress", "Completed": index + 1, "Total": len(pdf)})
        save(output, {"FileName": Path(source).name, "SourceSha256": fingerprint(source), "Engine": "CTranslate2 " + importlib.metadata.version("ctranslate2") + " CPU int8", "Model": "Argos English-Chinese 1.9 / OPUS-MT en-zh", "Pages": pages})
    finally:
        pdf.close()


def render(source, number, output):
    pdf = open_pdf(source)
    try:
        page = pdf[int(number)]
        width, height = page.get_size()
        scale = min(1.6, 2200 / max(width, height))
        bitmap = page.render(scale=scale)
        bitmap.to_pil().save(output, format="PNG")
        bitmap.close()
        page.close()
    finally:
        pdf.close()


def number_tokens(text):
    return collections.Counter(re.findall(r"(?<![A-Za-z])[-+]?\d+(?:[.,]\d+)*(?:[eE][-+]?\d+)?", text))


def translation_blocks(text):
    blocks = []
    current = []
    for line in text.splitlines():
        line = line.strip()
        letters = re.sub(r"[^A-Za-z]", "", line)
        heading = bool(letters) and len(line) < 120 and letters.isupper()
        if not line or heading:
            if current:
                blocks.append(" ".join(current))
                current = []
            if heading:
                blocks.append(line)
            continue
        current.append(line)
        if re.search(r"[.!?;][\"')\]]?$", line):
            blocks.append(" ".join(current))
            current = []
    if current:
        blocks.append(" ".join(current))
    return blocks


def translate(source, output, selection, component):
    import ctranslate2
    import sentencepiece as spm
    model = Path(component) / "model"
    translator = ctranslate2.Translator(str(model / "model"), device="cpu", compute_type="int8", inter_threads=1, intra_threads=min(4, os.cpu_count() or 1))
    # SentencePiece's native filename loader does not support every Windows Unicode path.
    tokenizer = spm.SentencePieceProcessor(model_proto=(model / "sentencepiece.model").read_bytes())
    document = json.loads(Path(output).read_text(encoding="utf-8"))
    if document["SourceSha256"] != fingerprint(source):
        raise ValueError("文献内容已改变，请重新导入。")
    selected = list(range(len(document["Pages"]))) if selection == "all" else [int(selection)]
    for completed, index in enumerate(selected):
        page = document["Pages"][index]
        text = page["OriginalText"]
        if text.strip():
            paragraphs = translation_blocks(text)
            translated = []
            for paragraph in paragraphs:
                normalized = re.sub(r"([A-Za-z])-\n([A-Za-z])", r"\1\2", paragraph)
                normalized = re.sub(r"\s+", " ", normalized).strip()
                if not normalized:
                    continue
                sentences = re.split(r"(?<=[.!?;])\s+(?=[A-Z0-9])", normalized)
                inputs = []
                for sentence in sentences:
                    tokens = tokenizer.encode(sentence, out_type=str)
                    inputs.extend(tokens[start:start + 160] for start in range(0, len(tokens), 160))
                outputs = []
                for start in range(0, len(inputs), 16):
                    results = translator.translate_batch(inputs[start:start + 16], beam_size=4, max_decoding_length=512)
                    outputs.extend(tokenizer.decode(item.hypotheses[0]).replace("\u2581", " ").strip() or "［本段未得到译文，请对照原文］" for item in results)
                translated.append("".join(outputs))
            page["ChineseText"] = "\n\n".join(translated)
            page["NumericReview"] = number_tokens(text) != number_tokens(page["ChineseText"])
            page["Status"] = "已翻译（数字需核对）" if page["NumericReview"] else "已翻译，仍需人工核对"
            if "［本段未得到译文" in page["ChineseText"] or not page["ChineseText"].strip():
                page["Status"] = "译文不完整，必须对照原文核对"
        save(output, document)
        emit({"Kind": "progress", "Completed": completed + 1, "Total": len(selected)})


def main():
    import ctranslate2
    import sentencepiece
    import pypdfium2
    socket.socket = no_network
    socket.create_connection = no_network
    command = sys.argv[1]
    if command == "probe":
        import ctranslate2
        import sentencepiece
        import pypdfium2
        emit({"Kind": "probe", "Ready": True, "Engine": "CTranslate2 " + ctranslate2.__version__ + " CPU", "Pdfium": importlib.metadata.version("pypdfium2")})
    elif command == "inspect":
        inspect(sys.argv[2], sys.argv[3])
    elif command == "render":
        render(sys.argv[2], sys.argv[3], sys.argv[4])
    elif command == "translate":
        translate(sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5])
    else:
        raise ValueError("不支持的离线任务。")


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        message = str(error) if isinstance(error, ValueError) else "离线处理失败。请核对组件是否完整、文献是否加密或损坏，以及本机运行库是否可用。"
        emit({"Kind": "error", "Message": message})
        sys.exit(2)
