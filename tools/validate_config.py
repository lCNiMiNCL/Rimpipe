#!/usr/bin/env python3
"""RimPipe config validation:
- all Defs XML well-formed
- every RimPipeDefOf field has a matching defName in Defs
- ChineseSimplified and English Keyed key sets are identical
- DefInjected tags are well-formed, reference existing DefNames, and are consistent across languages that ship DefInjected
"""
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def fail(msg):
    print(f"FAIL: {msg}")
    sys.exit(1)

def main():
    # 1. XML well-formed + collect defNames
    defs_dir = ROOT / "Defs"
    if not defs_dir.exists():
        fail("Defs/ directory not found")
    def_names = set()
    for xml_path in sorted(defs_dir.rglob("*.xml")):
        try:
            tree = ET.parse(xml_path)
        except ET.ParseError as e:
            fail(f"{xml_path.relative_to(ROOT)}: XML parse error: {e}")
        root = tree.getroot()
        for child in root:
            def_name = child.findtext("defName")
            if def_name:
                def_names.add(def_name.strip())

    # 2. RimPipeDefOf fields must exist
    defof_path = ROOT / "Source" / "RimPipe" / "RimPipeDefOf.cs"
    if not defof_path.exists():
        fail(f"missing {defof_path.relative_to(ROOT)}")
    text = defof_path.read_text(encoding="utf-8")
    fields = re.findall(r'public static\s+[A-Za-z0-9_.<>\[\],\?]+\s+(\w+)\s*(?:=|;)', text)
    missing = [name for name in fields if name not in def_names]
    if missing:
        fail(f"RimPipeDefOf references missing in Defs: {missing}")

    # 3. Keyed localization key sets equal
    langs_dir = ROOT / "Languages"
    if not langs_dir.exists():
        fail("Languages/ directory not found")
    key_sets = {}
    for keyed_path in sorted(langs_dir.rglob("Keyed/*.xml")):
        try:
            tree = ET.parse(keyed_path)
        except ET.ParseError as e:
            fail(f"{keyed_path.relative_to(ROOT)}: XML parse error: {e}")
        keys = set()
        for child in tree.getroot():
            if child.tag:
                keys.add(child.tag)
        lang = keyed_path.parent.parent.name
        key_sets[lang] = keys

    required_langs = {"ChineseSimplified", "English"}
    if not required_langs.issubset(key_sets.keys()):
        fail(f"expected ChineseSimplified and English Keyed files, got {sorted(key_sets.keys())}")
    base = None
    for lang, keys in key_sets.items():
        if base is None:
            base = keys
        elif base != keys:
            only_a = sorted(base - keys)
            only_b = sorted(keys - base)
            fail(f"Keyed mismatch for {lang}: missing={only_a} extra={only_b}")

    # 4. DefInjected XML well-formed + tag 指向的 DefName 必须存在于 Defs
    #    + 跨语言 DefInjected tag 集合一致（只比较实际提供 DefInjected 的语言，
    #      简中当前以 Defs 中文为基线，可没有 DefInjected 目录）。
    definjected_sets = {}
    for definjected_path in sorted(
            xml_path for xml_path in langs_dir.rglob("*.xml") if "DefInjected" in xml_path.parts):
        try:
            tree = ET.parse(definjected_path)
        except ET.ParseError as e:
            fail(f"{definjected_path.relative_to(ROOT)}: XML parse error: {e}")
        # 路径形如 Languages/<lang>/DefInjected/<DefType>/<file>.xml
        lang = definjected_path.parent.parent.parent.name
        keys = definjected_sets.setdefault(lang, set())
        for child in tree.getroot():
            tag = child.tag
            if "." not in tag:
                continue
            def_name = tag.split(".", 1)[0]
            if def_name not in def_names:
                fail(f"{definjected_path.relative_to(ROOT)}: DefInjected tag '{tag}' references missing DefName '{def_name}'")
            keys.add(tag)

    definjected_base = None
    for lang in sorted(definjected_sets):
        keys = definjected_sets[lang]
        if definjected_base is None:
            definjected_base = keys
        elif definjected_base != keys:
            only_a = sorted(definjected_base - keys)
            only_b = sorted(keys - definjected_base)
            fail(f"DefInjected mismatch for {lang}: missing={only_a} extra={only_b}")

    # 5. 核心数据封装守卫：运行时核心类不允许出现 public 可变字段（只读属性允许）。
    core_data_files = [
        "Source/RimPipe/Core/Container.cs",
        "Source/RimPipe/Core/Mapping.cs",
        "Source/RimPipe/Core/Port.cs",
        "Source/RimPipe/Core/ChemReactorBinding.cs",
        "Source/RimPipe/Comps/CompPipeNetworkMember.cs",
    ]
    public_field_pattern = re.compile(
        r"^\s*public\s+"
        r"(?!(?:const|static|readonly|override|partial|class|interface|enum|struct|sealed|abstract)\s)"
        r"[A-Za-z0-9_<>,.?\[\]]+\s+(\w+)\s*"
        r"(?:=\s*(?!>)[^=]+|;)\s*$"
    )
    bad_public_fields = []
    for rel in core_data_files:
        core_path = ROOT / rel
        if not core_path.exists():
            fail(f"missing {rel}")
        for line_no, line in enumerate(core_path.read_text(encoding="utf-8").splitlines(), 1):
            if public_field_pattern.match(line):
                bad_public_fields.append(f"{rel}:{line_no}: {line.strip()}")
    if bad_public_fields:
        fail("core data encapsulation: public mutable fields found:\n" + "\n".join(bad_public_fields))

    print(f"OK: {len(def_names)} defs, {len(fields)} DefOf fields, "
          f"{len(base) if base else 0} Keyed keys, "
          f"{len(definjected_base) if definjected_base else 0} DefInjected tags across {len(definjected_sets)} lang(s), "
          f"XML/DefOf/Keyed/DefInjected checks passed.")

if __name__ == "__main__":
    main()
