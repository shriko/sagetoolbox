"""Reconstruit fnfe_rules.zip, l'archive des règles France_RFE (FNFE) embarquée dans la Toolbox.

Usage : python build_rules_zip.py <dossier FNFE_RFE_INVOICE de France_RFE> [fnfe_rules.zip]

Contenu (chemins relatifs à FNFE_RFE_INVOICE, tels qu'attendus par Rulesets.cs) :
  - <profil>/schematron/*.sch  : catalogue des règles (identifiants, tests, messages)
  - <profil>/2xslt/*           : XSLT précompilés exécutés par Saxon + listes de codes *_codedb.xml
  - schémas XSD UBL 2.1, CII D22B, CDAR D22B et Factur-X
Source : dépôt France_RFE du FNFE, version 1.4.0.04 (licence Apache 2.0, voir LICENSE-France_RFE.txt).
"""
import sys
import zipfile
from pathlib import Path

PROFILE_DIRS = [
    "UBL/EN16931", "UBL/EXTENDED-CTC-FR",
    "CII/EN16931", "CII/EXTENDED-CTC-FR",
    "Factur-X/EN16931", "Factur-X/EXTENDED", "Factur-X/BASICWL",
    "CDAR",
]
XSD_DIRS = [
    "UBL/1xsd_UBL2.1", "CII/1xsd-CII_D22B_uncoupled", "CDAR/1xsd-CDAR_D22B_uncoupled",
    "Factur-X/EN16931/1xsd", "Factur-X/EXTENDED/1xsd", "Factur-X/BASICWL/1xsd",
]


def main() -> None:
    src = Path(sys.argv[1])
    out = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(__file__).with_name("fnfe_rules.zip")

    files: set[Path] = set()
    for profile in PROFILE_DIRS:
        files.update((src / profile / "schematron").glob("*.sch"))
        files.update(p for p in (src / profile / "2xslt").iterdir() if p.is_file())
    for xsd in XSD_DIRS:
        files.update(p for p in (src / xsd).rglob("*") if p.is_file())

    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in sorted(files):
            archive.write(path, path.relative_to(src).as_posix())
    print(f"{len(files)} fichiers -> {out} ({out.stat().st_size / 1e6:.2f} Mo)")


if __name__ == "__main__":
    main()
